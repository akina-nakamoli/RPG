using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Town Hall offers, progress, and one-time turn-in. Completed entries remain in the journal.</summary>
public class TownQuestBoardUI : MonoBehaviour
{
    [Serializable]
    public class QuestOffer
    {
        [Tooltip("Stable unique save identifier; do not rename after shipping saves.")]
        public string id;
        public string title;
        [TextArea(2, 5)] public string description;
        [TextArea(1, 3)] public string objective;
    }

    public static TownQuestBoardUI OpenBoard { get; private set; }
    private static int closedFrame = -1;
    public static bool BlocksInput => OpenBoard != null || Time.frameCount == closedFrame;
    public bool IsOpen { get; private set; }
    [SerializeField] private GameObject panel;
    [SerializeField] private GameObject interactionHint;
    [SerializeField] private TMP_Text hintText;
    [SerializeField] private Button availableButton, acceptedButton, closeButton, acceptButton;
    [SerializeField] private Button previousButton, nextButton;
    [SerializeField] private Button[] questButtons;
    [SerializeField] private TMP_Text[] questLabels;
    [SerializeField] private TMP_Text titleText, descriptionText, objectiveText, statusText, pageText, emptyText;
    [SerializeField] private TMP_Text acceptLabel;
    [SerializeField] private QuestOffer[] quests =
    {
        new QuestOffer { id = "town_goblin_01", title = "Goblin Hunt", description = "Goblins are threatening travelers near the village. Help the town make the roads safe again.", objective = "Defeat 3 Goblins." },
        new QuestOffer { id = "town_mushroom_01", title = "Mushroom Gathering", description = "The village kitchen is running low on fresh mushrooms.", objective = "Collect 5 Mushrooms for the kitchen." },
        new QuestOffer { id = "town_wood_01", title = "Timber Supplies", description = "The town builders need timber to repair the village fence.", objective = "Collect 10 Wood for the town." },
        new QuestOffer { id = "town_meat_01", title = "Meat Supplies", description = "The village cook needs fresh meat for the town feast.", objective = "Collect 3 Meat for the village." }
    };

    private readonly TownQuestProgress progress = new TownQuestProgress();
    private InventoryManager inventory;
    public event Action Changed;
    public IReadOnlyList<QuestOffer> Offers => quests;
    private readonly List<QuestOffer> visible = new List<QuestOffer>();
    private PlayerMovements player;
    private TaskHallInteraction owner;
    private bool showingAccepted;
    private int page, selected = -1;
    private bool initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { OpenBoard = null; closedFrame = -1; }

    public static TownQuestBoardUI FindOrCreate()
    {
        TownQuestBoardUI board = FindObjectOfType<TownQuestBoardUI>(true);
        if (board != null) return board;
        GameObject prefab = Resources.Load<GameObject>("TownQuestCanvas");
        if (prefab == null) { Debug.LogError("TownQuestCanvas prefab is missing from Resources."); return null; }
        GameObject instance = Instantiate(prefab);
        instance.name = "TownQuestCanvas";
        return instance.GetComponent<TownQuestBoardUI>();
    }

    private void Awake() => Initialize();
    private void Start() => BindInventory();

    private void BindInventory()
    {
        if (inventory != null) return;
        inventory = InventoryManager.GetOrCreate();
        inventory.Changed += NotifyProgressChanged;
    }

    private int InventoryAmount(string id)
    {
        string key = TownQuestProgress.ItemKey(id);
        if (key == null) return 0;
        BindInventory();
        return inventory.GetAmount(key);
    }

    public bool IsAccepted(string id) => progress.IsAccepted(id);
    public bool IsCompleted(string id) => progress.IsCompleted(id);
    public bool CanTurnIn(string id) => progress.CanTurnIn(id, InventoryAmount(id));
    public int CurrentProgress(string id) => progress.Progress(id, InventoryAmount(id));
    public string QuestStatus(string id) => IsCompleted(id) ? "COMPLETED"
        : id == TownQuestProgress.Escort && JameEscort.Stage < 2 ? (JameEscort.Health == 0 ? "FAILED - MEET JAME IN TOWN" : "MEET JAME IN TOWN")
        : CanTurnIn(id) ? "READY TO TURN IN" : "IN PROGRESS";
    public void NotifyEscortChanged() => NotifyProgressChanged();
    public string ProgressLabel(string id) => CurrentProgress(id) + " / " + TownQuestProgress.Target(id);

    public void RecordGoblinKill()
    {
        if (LocalGameSession.IsLoading || !progress.RecordGoblinKill()) return;
        NotifyProgressChanged();
    }

    private void NotifyProgressChanged()
    {
        if (IsOpen)
        {
            for (int i = 0; i < questLabels.Length; i++)
            {
                int index = page * questButtons.Length + i;
                if (index < visible.Count) questLabels[i].text = EntryLabel(visible[index]);
            }
            Select(selected);
        }
        Changed?.Invoke();
    }

    private string EntryLabel(QuestOffer quest) => quest.title + (showingAccepted ? "\n" + QuestStatus(quest.id) : "");
    private void Initialize()
    {
        if (initialized) return;
        var offers = new List<QuestOffer>(quests);
        if (!offers.Exists(q => q != null && q.id == TownQuestProgress.Escort))
            offers.Add(new QuestOffer { id = TownQuestProgress.Escort, title = "Escort jame", description = "Jame is waiting at the Barracks. Meet him at the door and confirm departure, then guide him through Mistwood Forest.", objective = "Escort jame to the forest exit and say farewell." });
        quests = offers.ToArray();
        initialized = true;
        panel.SetActive(false);
        interactionHint.SetActive(false);
        availableButton.onClick.AddListener(() => ChangeTab(false));
        acceptedButton.onClick.AddListener(() => ChangeTab(true));
        closeButton.onClick.AddListener(Close);
        acceptButton.onClick.AddListener(AcceptSelected);
        previousButton.onClick.AddListener(() => ChangePage(-1));
        nextButton.onClick.AddListener(() => ChangePage(1));
        for (int i = 0; i < questButtons.Length; i++)
        {
            int index = i;
            questButtons[i].onClick.AddListener(() => Select(page * questButtons.Length + index));
        }
    }

    public void ShowHint(bool show, KeyCode key)
    {
        if (show && !gameObject.activeSelf) gameObject.SetActive(true);
        Initialize();
        interactionHint.SetActive(show && !IsOpen);
        if (show) hintText.text = "[" + key + "]  TOWN TASKS";
    }
    public GameObject CloneInteractionHint(Transform parent, string label)
    {
        if (interactionHint == null) return null;
        GameObject clone = Instantiate(interactionHint, parent, false);
        clone.name = "JameInteractionHint";
        TMP_Text text = clone.GetComponentInChildren<TMP_Text>(true);
        if (text != null) text.text = label;
        clone.SetActive(false);
        return clone;
    }

    public bool TryOpen(TaskHallInteraction source, PlayerMovements visitor)
    {
        if (visitor == null || visitor.InteractionLocked || IsOpen || OpenBoard != null || SettingsUI.BlocksGameplay) return false;
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        Initialize();
        if (!isActiveAndEnabled) return false;
        owner = source; player = visitor; IsOpen = true; OpenBoard = this;
        Player_Combat combat = player.GetComponent<Player_Combat>();
        if (combat != null) combat.FinishAttack();
        player.SetInteractionLocked(true);
        GameMenuUI menu = FindObjectOfType<GameMenuUI>();
        if (menu != null) { menu.ClosePanels(); menu.CloseMenu(); }
        interactionHint.SetActive(false); panel.SetActive(true);
        statusText.text = "Choose a notice to view its details.";
        ChangeTab(false);
        return true;
    }

    private void Update()
    {
        if (!IsOpen) return;
        if (player == null || !player.gameObject.activeInHierarchy || owner == null || !owner.isActiveAndEnabled)
        { Close(); return; }
        if (SettingsUI.BlocksGameplay) return;
        if (Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    public void Close()
    {
        if (!IsOpen) return;
        closedFrame = Time.frameCount;
        IsOpen = false;
        if (OpenBoard == this) OpenBoard = null;
        if (player != null) player.SetInteractionLocked(false);
        player = null; owner = null;
        panel.SetActive(false);
        interactionHint.SetActive(false);
    }
    private void OnDisable() { Close(); if (interactionHint != null) interactionHint.SetActive(false); }
    private void OnDestroy()
    {
        if (OpenBoard == this) OpenBoard = null;
        if (inventory != null) inventory.Changed -= NotifyProgressChanged;
    }

    private void ChangeTab(bool journal)
    {
        showingAccepted = journal; page = 0;
        RefreshOffers();
    }
    private void RefreshOffers()
    {
        visible.Clear();
        var seen = new HashSet<string>();
        foreach (QuestOffer quest in quests)
        {
            if (quest == null || string.IsNullOrWhiteSpace(quest.id) || !seen.Add(quest.id)) continue;
            if (progress.IsAccepted(quest.id) == showingAccepted) visible.Add(quest);
        }
        page = Mathf.Clamp(page, 0, Mathf.Max(0, (visible.Count - 1) / questButtons.Length));
        RefreshPage();
    }
    private void ChangePage(int offset)
    {
        page = Mathf.Clamp(page + offset, 0, Mathf.Max(0, (visible.Count - 1) / questButtons.Length));
        RefreshPage();
    }
    private void RefreshPage()
    {
        availableButton.interactable = showingAccepted;
        acceptedButton.interactable = !showingAccepted;
        for (int i = 0; i < questButtons.Length; i++)
        {
            int index = page * questButtons.Length + i;
            questButtons[i].gameObject.SetActive(index < visible.Count);
            if (index < visible.Count) questLabels[i].text = EntryLabel(visible[index]);
        }
        previousButton.interactable = page > 0;
        nextButton.interactable = (page + 1) * questButtons.Length < visible.Count;
        pageText.text = (page + 1) + " / " + Mathf.Max(1, Mathf.CeilToInt((float)visible.Count / questButtons.Length));
        emptyText.gameObject.SetActive(visible.Count == 0);
        emptyText.text = showingAccepted ? "No accepted tasks yet." : "All town tasks have been accepted.";
        Select(visible.Count == 0 ? -1 : page * questButtons.Length);
    }
    private void Select(int index)
    {
        selected = index;
        bool valid = index >= 0 && index < visible.Count;
        QuestOffer quest = valid ? visible[index] : null;
        titleText.text = valid ? quest.title : "TOWN NOTICE BOARD";
        descriptionText.text = valid ? quest.description : "Select the other tab to browse your tasks.";
        objectiveText.text = valid ? "OBJECTIVE\n" + quest.objective : "";
        if (valid && IsAccepted(quest.id)) objectiveText.text += "\n" + ProgressLabel(quest.id) + " - " + QuestStatus(quest.id);
        acceptButton.interactable = valid && (!IsAccepted(quest.id) || CanTurnIn(quest.id));
        acceptLabel.text = !valid || !IsAccepted(quest.id) ? "ACCEPT" : IsCompleted(quest.id) ? "COMPLETED"
            : CanTurnIn(quest.id) ? "TURN IN" : "IN PROGRESS";
        for (int i = 0; i < questButtons.Length; i++)
            questButtons[i].interactable = page * questButtons.Length + i != selected;
    }
    private void AcceptSelected()
    {
        if (!IsOpen || SettingsUI.BlocksGameplay || selected < 0 || selected >= visible.Count) return;
        QuestOffer quest = visible[selected];
        // This action is only available in the open Hall interaction, never in QuestCanvas.
        if (owner == null || player == null) return;
        if (IsAccepted(quest.id))
        {
            BindInventory();
            if (!progress.TryTurnIn(quest.id, InventoryAmount(quest.id), inventory.TryRemove)) return;
            statusText.text = "Completed: " + quest.title;
            NotifyProgressChanged();
        }
        else
        {
            if (!progress.Accept(quest.id)) return;
            statusText.text = "Accepted: " + quest.title;
            RefreshOffers();
            Changed?.Invoke();
        }
        LocalGameSession session = LocalGameSession.Instance;
        if (session != null && session.CanSave && !session.TrySave())
            statusText.text += "\nSave failed. Use F5 to retry before leaving.";
    }

    public string[] CaptureAcceptedQuests() => progress.AcceptedIds();
    public void CompleteEscort() { if (progress.CompleteEscort()) NotifyProgressChanged(); }
    public SavedTownQuest[] CaptureQuestStates() => progress.Capture();
    public void RestoreAcceptedQuests(string[] ids, SavedTownQuest[] states = null)
    {
        progress.Restore(ids, states);
        if (IsOpen) RefreshOffers();
        Changed?.Invoke();
    }
}
