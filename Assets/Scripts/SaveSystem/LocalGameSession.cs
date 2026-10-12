using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>One authenticated account and one selected save slot per session.</summary>
public class LocalGameSession : MonoBehaviour
{
    public static LocalGameSession Instance { get; private set; }
    public static bool IsLoading => Instance != null && Instance.loading;
    public static bool BlocksGameplay => DeathPanelUI.IsOpen || (Instance != null && (Instance.loading || Instance.leaveConfirmation != null));
    public LocalAccount Account { get; private set; }
    public int Slot { get; private set; }
    public LocalAccountStore Store { get; private set; }
    public string Status { get; private set; }
    public bool HasActiveSlot => Account != null && Slot > 0;
    public bool CanSave => ready && !loading && !DeathPanelUI.IsOpen && !SceneTravel.IsTravelling && leaveConfirmation == null && HasActiveSlot;
    private bool loading, ready;
    private float nextAutosave;
    private float nextCloudSync;
    private ItemSO[] catalog;
    private Sprite paper, button;
    private TMP_FontAsset font;
    private GameObject leaveConfirmation;
    private float timeScaleBeforeConfirmation;

    public static LocalGameSession GetOrCreate()
    {
        if (Instance == null) new GameObject("LocalGameSession").AddComponent<LocalGameSession>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Store = new LocalAccountStore(Path.Combine(Application.persistentDataPath, "LocalProfiles"));
    }

    public void Configure(ItemSO[] items, Sprite panelSprite, Sprite buttonSprite, TMP_FontAsset bodyFont)
    {
        catalog = items ?? new ItemSO[0]; paper = panelSprite; button = buttonSprite; font = bodyFont;
    }

    public async Task AuthenticateAsync(string username, string password, bool register, string invitationCode = null)
    {
        if (loading || ready) throw new InvalidOperationException("Return to the account screen first.");
        loading = true;
        try { Account = await Store.AuthenticateAsync(username, password, register, invitationCode); }
        finally { loading = false; }
        Slot = 0;
        SetStatus("Choose a save slot.");
    }

    public async Task LogoutAsync()
    {
        if (ready || loading) return;
        loading = true;
        try { await Store.LogoutAsync(); }
        finally { loading = false; }
        Account = null; Slot = 0;
        SetStatus("Signed out.");
    }

    public void BeginSlot(int slot, bool continueGame)
    {
        if (loading || Account == null) return;
        if (!Application.CanStreamedLevelBeLoaded("SampleScene"))
            throw new InvalidOperationException("SampleScene is missing from Build Settings.");
        GameSaveData save = null;
        if (continueGame) save = Store.Load(Account.id, slot);
        else if (Store.HasSave(Account.id, slot))
            throw new InvalidOperationException("This slot already has a save. Choose Continue or an empty slot.");
        if (save != null && !Application.CanStreamedLevelBeLoaded(save.scene))
            throw new InvalidOperationException("Saved scene is missing from Build Settings: " + save.scene);
        Slot = slot;
        StartCoroutine(EnterGame(save));
    }

    public bool TryReloadLastSave(out string error)
    {
        error = null;
        if (loading || SceneTravel.IsTravelling) { error = "Already loading. Please wait."; return false; }
        if (!HasActiveSlot) { error = "No account save is selected. Return to the login page."; return false; }
        try
        {
            // Use the latest local-first snapshot, not a network request or death-time save.
            GameSaveData save = Store.Load(Account.id, Slot);
            if (!Application.CanStreamedLevelBeLoaded(save.scene))
                throw new InvalidOperationException("Saved scene is missing from Build Settings: " + save.scene);
            StartCoroutine(EnterGame(save));
            return true;
        }
        catch (Exception ex) { error = "Could not load save: " + ex.Message; SetStatus(error); return false; }
    }

    public bool TryRestartTestScene(out string error)
    {
        error = null;
        if (loading || SceneTravel.IsTravelling) { error = "Already loading. Please wait."; return false; }
        if (HasActiveSlot) { error = "Load your save instead; it will not be overwritten."; return false; }
        string scene = SceneManager.GetActiveScene().name;
        if (!Application.CanStreamedLevelBeLoaded(scene)) { error = "This scene is missing from Build Settings."; return false; }
        StartCoroutine(EnterGame(null, scene));
        return true;
    }

    public bool TryReturnAfterDeath(out string error)
    {
        error = null;
        if (loading || SceneTravel.IsTravelling) { error = "Already loading. Please wait."; return false; }
        if (!Application.CanStreamedLevelBeLoaded("LoginScene")) { error = "LoginScene is missing from Build Settings."; return false; }
        // Intentionally bypass TrySave/SaveAndReturn: a defeated run must never be saved.
        StartCoroutine(ReturnToLogin());
        return true;
    }

    private IEnumerator EnterGame(GameSaveData save, string restartScene = "SampleScene")
    {
        loading = true; ready = false;
        SceneTravel.ResetVisitedScenes();
        ForestMazeProgress.Restore(save);
        TargetRangeGame.Restore(save);
        JameEscort.Restore(save != null ? save.escortJameStage : 0, save != null ? save.escortJameHealth : 10);
        SetStatus("Loading...");
        StatsManager.ReleaseForSceneLoad();
        Time.timeScale = 0f;
        // Destroy persistent stats before scene Awake methods create the new scene defaults.
        yield return null;
        AsyncOperation operation = null;
        Exception sceneFailure = null;
        try { operation = SceneManager.LoadSceneAsync(save != null ? save.scene : restartScene); }
        catch (Exception ex) { sceneFailure = ex; }
        if (operation == null)
        {
            loading = false;
            Time.timeScale = DeathPanelUI.IsOpen ? 0f : 1f;
            SetStatus("Could not load scene: " + (sceneFailure != null ? sceneFailure.Message : "No loading operation was created."));
            yield break;
        }
        yield return operation;
        yield return null; // Inventory/UI Start methods must finish before restoration.
        Exception failure = null;
        try
        {
            PlayerMovements player = FindObjectOfType<PlayerMovements>();
            if (player == null) throw new InvalidOperationException("Player was not found in the loaded scene.");
            InventoryManager inventory = InventoryManager.GetOrCreate();
            if (save != null)
            {
                StatsManager.GetOrCreate().RestoreProgress(save);
                inventory.RestoreItems(save.items, catalog);
                TownQuestBoardUI questBoard = TownQuestBoardUI.FindOrCreate();
                if (questBoard != null) questBoard.RestoreAcceptedQuests(save.acceptedTownQuests, save.townQuestStates);
                player.TeleportTo(new Vector2(save.x, save.y));
                Vector3 position = player.transform.position; position.z = save.z; player.transform.position = position;
                Player_Health health = player.GetComponent<Player_Health>();
                if (health == null) throw new InvalidOperationException("Player health component is missing.");
                health.RestoreHealth(save.health);
                SpriteRenderer renderer = player.GetComponent<SpriteRenderer>();
                if (renderer != null) renderer.sortingOrder = save.sortingOrder;
                WorldDepthSort.Ensure(player.gameObject);
                player.GetComponent<WorldDepthSort>().RestoreElevation(save.sortingOrder);
                var completed = new HashSet<string>(save.completedGreetings);
                foreach (NPCDialogue npc in FindObjectsOfType<NPCDialogue>(true))
                    npc.RestoreGreeting(completed.Contains(npc.PersistentGreetingKey));
                var terrain = TerrainColliders();
                foreach (SavedCollider state in save.terrainColliders)
                    if (terrain.TryGetValue(state.key, out Collider2D collider)) collider.enabled = state.enabled;
                if (save.sortingOrder < 16000)
                    foreach (High_Enter entry in FindObjectsOfType<High_Enter>(true))
                        if (entry.IsHighGroundActive()) { player.GetComponent<WorldDepthSort>().SetElevated(true); break; }
                Physics2D.SyncTransforms();
            }
        }
        catch (Exception ex) { failure = ex; }
        if (failure != null)
        {
            SetStatus("Could not restore save: " + failure.Message);
            Debug.LogWarning(Status);
            yield return ReturnToLogin();
            yield break; // Never auto-save a partially restored game.
        }
        loading = false; ready = true; Time.timeScale = 1f;
        nextAutosave = Time.unscaledTime + 60f;
        if (save == null && HasActiveSlot) TrySave();
        else if (save == null) SetStatus("Scene restarted in test mode. Log in to use saved progress.");
        else SetStatus("Loaded slot " + Slot + ". Autosave every 60 seconds.");
    }

    public bool TrySave()
    {
        if (!CanSave) { SetStatus("No active save slot."); return false; }
        try
        {
            PlayerMovements player = FindObjectOfType<PlayerMovements>();
            Player_Health health = player != null ? player.GetComponent<Player_Health>() : null;
            if (health == null || health.CurrentHealth <= 0)
                throw new InvalidOperationException("Cannot save while the player is defeated. The previous save is safe.");
            StatsManager stats = StatsManager.GetOrCreate();
            var greetings = new List<string>();
            foreach (NPCDialogue npc in FindObjectsOfType<NPCDialogue>(true))
                if (npc.GreetingCompleted) greetings.Add(npc.PersistentGreetingKey);
            var colliders = new List<SavedCollider>();
            foreach (var pair in TerrainColliders()) colliders.Add(new SavedCollider { key = pair.Key, enabled = pair.Value.enabled });
            SpriteRenderer renderer = player.GetComponent<SpriteRenderer>();
            TownQuestBoardUI questBoard = FindObjectOfType<TownQuestBoardUI>(true);
            Store.Save(new GameSaveData
            {
                accountId = Account.id, slot = Slot, savedUtc = DateTime.UtcNow.ToString("O"),
                scene = SceneManager.GetActiveScene().name,
                x = player.transform.position.x, y = player.transform.position.y, z = player.transform.position.z,
                health = health.CurrentHealth, level = stats.playerLevel, experience = stats.playerExperience,
                skillPoints = stats.skillPoints, skills = stats.CaptureSkillLevels(),
                items = InventoryManager.GetOrCreate().CaptureItems(), completedGreetings = greetings.ToArray(),
                acceptedTownQuests = questBoard != null ? questBoard.CaptureAcceptedQuests() : new string[0],
                townQuestStates = questBoard != null ? questBoard.CaptureQuestStates() : new SavedTownQuest[0],
                defeatedForestGoblins = ForestMazeProgress.Capture(),
                forestChestOpened = ForestMazeProgress.ChestOpened,
                escortJameStage = JameEscort.Stage,
                escortJameHealth = JameEscort.Health,
                targetRangeBestScore = TargetRangeGame.BestScore,
                targetRangeHeroUnlocked = TargetRangeGame.HeroUnlocked,
                archerSelected = ArcherHero.Selected,
                terrainColliders = colliders.ToArray(), sortingOrder = renderer != null ? renderer.sortingOrder : 10
            });
            SetStatus(Store.UsesDatabase ? Store.SyncStatus : "Slot " + Slot + " saved at " + DateTime.Now.ToString("HH:mm:ss"));
            if (Store.UsesDatabase) SynchronizeCloud();
            return true;
        }
        catch (Exception ex) { SetStatus("Save failed: " + ex.Message); Debug.LogWarning(Status); return false; }
    }

    public void SaveAndReturn()
    {
        if (loading) return;
        if (!Application.CanStreamedLevelBeLoaded("LoginScene")) { SetStatus("LoginScene is missing from Build Settings."); return; }
        if (!TrySave()) { ShowLeaveConfirmation(); return; }
        StartCoroutine(ReturnToLogin());
    }

    private void ShowLeaveConfirmation()
    {
        if (leaveConfirmation != null) return;
        var ui = new LoginUIFactory(font, paper, button);
        RectTransform canvas = ui.Canvas("LeaveWithoutSavingCanvas", 200);
        leaveConfirmation = canvas.gameObject;
        timeScaleBeforeConfirmation = Time.timeScale;
        Time.timeScale = 0f;
        RectTransform blocker = ui.Panel(canvas, "Blocker", Vector2.zero, Vector2.zero);
        blocker.anchorMin = Vector2.zero; blocker.anchorMax = Vector2.one; blocker.sizeDelta = Vector2.zero;
        var image = blocker.GetComponent<UnityEngine.UI.Image>(); image.sprite = null; image.color = new Color(0, 0, 0, 0.75f);
        RectTransform card = ui.Panel(canvas, "Confirmation", Vector2.zero, new Vector2(820, 400));
        ui.Text(card, "Title", "RETURN WITHOUT SAVING?", new Vector2(0, 110), new Vector2(740, 70), 32);
        ui.Text(card, "Warning", "Saving failed. Your previous save is preserved.\nProgress since the last save will be lost.", Vector2.zero, new Vector2(720, 100), 25);
        ui.Button(card, "Leave", "LEAVE", new Vector2(-185, -120), new Vector2(300, 70)).onClick.AddListener(() =>
        {
            Destroy(leaveConfirmation); leaveConfirmation = null;
            Time.timeScale = timeScaleBeforeConfirmation;
            StartCoroutine(ReturnToLogin());
        });
        ui.Button(card, "Cancel", "CANCEL", new Vector2(185, -120), new Vector2(300, 70)).onClick.AddListener(() =>
        {
            Destroy(leaveConfirmation); leaveConfirmation = null;
            Time.timeScale = timeScaleBeforeConfirmation;
        });
    }

    private IEnumerator ReturnToLogin()
    {
        ready = false; loading = true;
        if (SettingsUI.Instance != null) SettingsUI.Instance.Close();
        StatsManager.ReleaseForSceneLoad();
        yield return null;
        AsyncOperation operation = null;
        Exception failure = null;
        try { operation = SceneManager.LoadSceneAsync("LoginScene"); }
        catch (Exception ex) { failure = ex; }
        if (operation == null)
        {
            loading = false;
            Time.timeScale = DeathPanelUI.IsOpen ? 0f : 1f;
            SetStatus("Could not return to login: " + (failure != null ? failure.Message : "Scene load did not start."));
            yield break;
        }
        yield return operation;
        Slot = 0; loading = false; Time.timeScale = 1f;
    }

    private void Update()
    {
        if (Store != null && Store.UsesDatabase && Account != null && !loading && Time.unscaledTime >= nextCloudSync)
        {
            nextCloudSync = Time.unscaledTime + 15f;
            SynchronizeCloud();
        }
        if (!CanSave) return;
        if (Input.GetKeyDown(KeyCode.F5)) TrySave();
        if (Time.unscaledTime >= nextAutosave)
        {
            nextAutosave = Time.unscaledTime + 60f;
            PlayerMovements player = FindObjectOfType<PlayerMovements>();
            if (player != null && !player.InteractionLocked) TrySave();
        }
    }

    private void OnApplicationPause(bool paused) { if (paused && CanSave) TrySave(); }
    private async void SynchronizeCloud()
    {
        try { await Store.SyncAsync(); if (!string.IsNullOrEmpty(Store.SyncStatus)) SetStatus(Store.SyncStatus); }
        catch (Exception ex) { SetStatus("Local saved; cloud sync failed: " + ex.Message); }
    }
    public void StopSavingForExit() => ready = false;
    private void OnApplicationQuit() { if (CanSave) TrySave(); }
    private void OnDestroy() { if (Instance == this) Instance = null; }
    private void SetStatus(string value) { Status = value; }

    // Hierarchy keys distinguish NPC instances sharing the same prefab/name.
    public static string ObjectKey(Transform node)
    {
        string key = node.name + "[" + node.GetSiblingIndex() + "]";
        while (node.parent != null) { node = node.parent; key = node.name + "[" + node.GetSiblingIndex() + "]/" + key; }
        return key;
    }

    private static Dictionary<string, Collider2D> TerrainColliders()
    {
        var result = new Dictionary<string, Collider2D>();
        foreach (High_Enter zone in FindObjectsOfType<High_Enter>(true))
        {
            AddTerrain(result, zone.mountainColliders); AddTerrain(result, zone.boundaryColliders);
        }
        foreach (High_Exit zone in FindObjectsOfType<High_Exit>(true))
        {
            AddTerrain(result, zone.mountainColliders); AddTerrain(result, zone.boundaryColliders);
        }
        return result;
    }

    private static void AddTerrain(Dictionary<string, Collider2D> result, Collider2D[] colliders)
    {
        if (colliders == null) return;
        foreach (Collider2D c in colliders)
            if (c != null) result[ObjectKey(c.transform) + ":" + Array.IndexOf(c.GetComponents<Collider2D>(), c)] = c;
    }
}
