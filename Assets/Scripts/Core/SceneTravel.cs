using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Preserves gameplay progress even when testing directly without a login.</summary>
public class SceneTravel : MonoBehaviour
{
    public static bool IsTravelling { get; private set; }
    private static SceneTravel instance;
    private readonly Dictionary<string, GameSaveData> visited = new Dictionary<string, GameSaveData>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; IsTravelling = false; }

    public static void ResetVisitedScenes()
    {
        if (instance != null) instance.visited.Clear();
    }

    public static void Begin(string scene, Vector2 arrival)
    {
        if (IsTravelling || LocalGameSession.IsLoading || DeathPanelUI.IsOpen) return;
        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogError("Travel destination is missing from Build Settings: " + scene);
            return;
        }
        if (instance == null) new GameObject("SceneTravel").AddComponent<SceneTravel>();
        instance.StartCoroutine(instance.Travel(scene, arrival));
    }

    private void Awake()
    {
        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Do not carry NPC state between different accounts or save slots.
        if (scene.name == "LoginScene") { visited.Clear(); ForestMazeProgress.Reset(); JameEscort.Restore(0); }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this) { instance = null; IsTravelling = false; }
    }

    private IEnumerator Travel(string destination, Vector2 arrival)
    {
        PlayerMovements player = FindObjectOfType<PlayerMovements>();
        Player_Health health = player != null ? player.GetComponent<Player_Health>() : null;
        if (health == null || health.CurrentHealth <= 0) yield break;
        StatsManager stats = StatsManager.GetOrCreate();
        TownQuestBoardUI board = TownQuestBoardUI.FindOrCreate();
        GameSaveData progress = new GameSaveData
        {
            scene = SceneManager.GetActiveScene().name,
            health = health.CurrentHealth, level = stats.playerLevel, experience = stats.playerExperience,
            skillPoints = stats.skillPoints, skills = stats.CaptureSkillLevels(),
            items = InventoryManager.GetOrCreate().CaptureItems(),
            acceptedTownQuests = board != null ? board.CaptureAcceptedQuests() : new string[0],
            townQuestStates = board != null ? board.CaptureQuestStates() : new SavedTownQuest[0]
        };
        var greetings = new List<string>();
        foreach (NPCDialogue npc in FindObjectsOfType<NPCDialogue>(true))
            if (npc.GreetingCompleted) greetings.Add(npc.PersistentGreetingKey);
        progress.completedGreetings = greetings.ToArray();
        visited[progress.scene] = progress;
        ItemSO[] catalog = Resources.FindObjectsOfTypeAll<ItemSO>();
        IsTravelling = true;
        player.SetInteractionLocked(true);
        float previousScale = Time.timeScale;
        Time.timeScale = 0f;
        AsyncOperation operation;
        try { operation = SceneManager.LoadSceneAsync(destination); }
        catch (Exception ex)
        {
            player.SetInteractionLocked(false);
            IsTravelling = false;
            Time.timeScale = previousScale;
            Debug.LogError("Could not load travel destination: " + ex.Message);
            yield break;
        }
        if (operation == null)
        {
            player.SetInteractionLocked(false);
            IsTravelling = false;
            Time.timeScale = previousScale;
            yield break;
        }
        yield return operation;
        yield return null;
        try
        {
            player = FindObjectOfType<PlayerMovements>();
            if (player == null) throw new InvalidOperationException("Destination has no player.");
            stats = StatsManager.GetOrCreate();
            stats.RestoreProgress(progress);
            InventoryManager.GetOrCreate().RestoreItems(progress.items, catalog);
            board = TownQuestBoardUI.FindOrCreate();
            if (board != null) board.RestoreAcceptedQuests(progress.acceptedTownQuests, progress.townQuestStates);
            player.GetComponent<Player_Health>().RestoreHealth(progress.health);
            player.TeleportTo(arrival);
            player.SetInteractionLocked(false);
            if (visited.TryGetValue(destination, out GameSaveData prior))
            {
                var completed = new HashSet<string>(prior.completedGreetings);
                foreach (NPCDialogue npc in FindObjectsOfType<NPCDialogue>(true))
                    npc.RestoreGreeting(completed.Contains(npc.PersistentGreetingKey));
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Scene travel restoration failed; previous disk save is unchanged: " + ex.Message);
            if (LocalGameSession.Instance != null) LocalGameSession.Instance.StopSavingForExit();
        }
        finally { IsTravelling = false; Time.timeScale = previousScale; }
    }
}
