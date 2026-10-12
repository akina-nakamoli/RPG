using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Scene-authored defeat modal. It never saves a defeated run.</summary>
public sealed class DeathPanelUI : MonoBehaviour
{
    public GameObject panel;
    public GameObject backdrop;
    public TMP_Text message;
    public TMP_Text status;
    public Button loadSave;
    public TMP_Text loadSaveLabel;
    public Button returnToLogin;
    public static bool IsOpen { get; private set; }
    private static DeathPanelUI current;
    private bool busy;
    private float previousTimeScale;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { IsOpen = false; current = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "SampleScene" || scene.name == "WildernessForest") EnsureForScene(scene);
    }

    private static DeathPanelUI EnsureForScene(Scene scene)
    {
        Transform manager = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            DeathPanelUI existing = root.GetComponentInChildren<DeathPanelUI>(true);
            if (existing != null) { existing.gameObject.SetActive(true); return existing; }
            if (root.name == "UIManager") manager = root.transform;
        }
        GamePrefabCatalog catalog = GamePrefabCatalog.Load();
        if (catalog == null || catalog.deathPanel == null)
        {
            Debug.LogError("DeathPanel prefab is missing from Resources/GamePrefabs.");
            return null;
        }
        GameObject instance = Instantiate(catalog.deathPanel, manager);
        instance.name = "DeathPanel";
        if (manager == null) SceneManager.MoveGameObjectToScene(instance, scene);
        return instance.GetComponent<DeathPanelUI>();
    }

    private void Awake()
    {
        if (!Application.isPlaying) return;
        panel.SetActive(false);
        backdrop.SetActive(false);
    }

    private void OnEnable()
    {
        loadSave.onClick.AddListener(Reload);
        returnToLogin.onClick.AddListener(ReturnToLogin);
    }

    private void OnDisable()
    {
        loadSave.onClick.RemoveListener(Reload);
        returnToLogin.onClick.RemoveListener(ReturnToLogin);
        Release();
    }

    private void OnDestroy() => Release();

    private void Release()
    {
        if (current != this) return;
        current = null;
        IsOpen = false;
        if (!LocalGameSession.IsLoading && !SceneTravel.IsTravelling) Time.timeScale = previousTimeScale;
    }

    public static void Show(Player_Health defeatedPlayer)
    {
        if (IsOpen || defeatedPlayer == null) return;
        DeathPanelUI ui = EnsureForScene(defeatedPlayer.gameObject.scene);
        if (ui == null) return;
        foreach (TargetRangeGame range in FindObjectsOfType<TargetRangeGame>()) range.Close();
        if (SettingsUI.Instance != null) SettingsUI.Instance.Close();
        foreach (TownQuestBoardUI board in FindObjectsOfType<TownQuestBoardUI>()) board.Close();
        foreach (VillageEntryPrompt prompt in FindObjectsOfType<VillageEntryPrompt>()) prompt.Close();
        foreach (DialogueUI dialogue in FindObjectsOfType<DialogueUI>(true))
            if (dialogue.IsPlaying) dialogue.gameObject.SetActive(false); // Cancel; do not complete/reward the dialogue.
        GameMenuUI menu = FindObjectOfType<GameMenuUI>();
        if (menu != null) { menu.ClosePanels(); menu.CloseMenu(); }
        PlayerMovements movement = defeatedPlayer.GetComponent<PlayerMovements>();
        if (movement != null) movement.SetInteractionLocked(true);
        Player_Combat combat = defeatedPlayer.GetComponent<Player_Combat>();
        if (combat != null) combat.FinishAttack();
        current = ui;
        IsOpen = true;
        ui.previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        ui.SetBusy(false);
        ui.backdrop.SetActive(true);
        ui.panel.SetActive(true);
        LocalGameSession session = LocalGameSession.Instance;
        bool hasSlot = session != null && session.HasActiveSlot;
        ui.loadSaveLabel.text = hasSlot ? "LOAD LAST SAVE" : "RESTART SCENE";
        ui.message.text = hasSlot
            ? "Return to your latest manual or automatic save.\nProgress since that save will be lost."
            : "No account save is selected in this test run.\nRestart the scene, or log in to use save slots.";
        ui.status.text = hasSlot ? "Your previous save is safe. SLOT " + session.Slot : "Test mode: restarting does not create or overwrite a save.";
    }

    private void Reload()
    {
        if (busy || !IsOpen) return;
        SetBusy(true);
        status.text = "Loading...";
        LocalGameSession session = LocalGameSession.GetOrCreate();
        string error;
        bool accepted = session.HasActiveSlot ? session.TryReloadLastSave(out error) : session.TryRestartTestScene(out error);
        if (!accepted) { SetBusy(false); status.text = error + "\nThe previous save was not changed."; }
    }

    private void ReturnToLogin()
    {
        if (busy || !IsOpen) return;
        SetBusy(true);
        status.text = "Returning to the login page...";
        if (!LocalGameSession.GetOrCreate().TryReturnAfterDeath(out string error))
        { SetBusy(false); status.text = error; }
    }

    private void Update()
    {
        // A scene-load exception leaves this modal alive: re-enable its buttons for retry/return.
        if (current != this || !busy || LocalGameSession.IsLoading) return;
        SetBusy(false);
        status.text = LocalGameSession.Instance != null ? LocalGameSession.Instance.Status : "Loading failed. Please retry.";
    }

    private void SetBusy(bool value)
    {
        busy = value;
        loadSave.interactable = returnToLogin.interactable = !value;
    }
}
