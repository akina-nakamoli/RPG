using UnityEngine;
using UnityEngine.UI;

/// <summary>Connects the HUD menu to the existing stats and skills canvases.</summary>
public class GameMenuUI : MonoBehaviour
{
    [Header("Menu")]
    [SerializeField] private Canvas menuCanvas;
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private Button menuButton;
    [SerializeField] private Button shrinkButton;
    [SerializeField] private Button statsButton;
    [SerializeField] private Button skillsButton;
    [SerializeField] private Button questButton;

    [Header("Existing Panels")]
    [SerializeField] private Canvas statsCanvas;
    [SerializeField] private GameObject statsPanel;
    [SerializeField] private Canvas skillsCanvas;
    [SerializeField] private GameObject skillsPanel;
    [SerializeField] private Canvas questCanvas;
    [SerializeField] private GameObject questPanel;

    private void Awake()
    {
        ConfigureQuestPanel();
        if (menuCanvas != null)
        {
            menuCanvas.gameObject.SetActive(true);
            menuCanvas.enabled = true;
            KeepMenuAbovePanels();
        }
        SetMenuExpanded(false);
        ClosePanels();
        if (questButton != null) questButton.interactable = questCanvas != null && questPanel != null;
    }

    private void OnEnable()
    {
        if (menuButton != null) menuButton.onClick.AddListener(OpenMenu);
        if (shrinkButton != null) shrinkButton.onClick.AddListener(CloseMenu);
        if (statsButton != null) statsButton.onClick.AddListener(ToggleStats);
        if (skillsButton != null) skillsButton.onClick.AddListener(ToggleSkills);
        if (questButton != null) questButton.onClick.AddListener(ToggleQuests);
    }

    private void OnDisable()
    {
        if (menuButton != null) menuButton.onClick.RemoveListener(OpenMenu);
        if (shrinkButton != null) shrinkButton.onClick.RemoveListener(CloseMenu);
        if (statsButton != null) statsButton.onClick.RemoveListener(ToggleStats);
        if (skillsButton != null) skillsButton.onClick.RemoveListener(ToggleSkills);
        if (questButton != null) questButton.onClick.RemoveListener(ToggleQuests);
    }

    private void Update()
    {
        if (SettingsUI.BlocksGameplay || TownQuestBoardUI.BlocksInput) return;
        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        if (IsPanelOpen(statsCanvas, statsPanel) || IsPanelOpen(skillsCanvas, skillsPanel) || IsPanelOpen(questCanvas, questPanel))
            ClosePanels();
        else if (menuPanel != null && menuPanel.activeSelf)
            CloseMenu();
    }

    public void OpenMenu() => SetMenuExpanded(true);
    public void CloseMenu() => SetMenuExpanded(false);

    private void SetMenuExpanded(bool expanded)
    {
        if (menuPanel != null) menuPanel.SetActive(expanded);
        if (menuButton != null) menuButton.gameObject.SetActive(!expanded);
    }

    public void ToggleStats()
    {
        bool open = !IsPanelOpen(statsCanvas, statsPanel);
        SetPanelVisible(questCanvas, questPanel, false);
        SetPanelVisible(skillsCanvas, skillsPanel, false);
        SetPanelVisible(statsCanvas, statsPanel, open);
    }

    public void ToggleSkills()
    {
        bool open = !IsPanelOpen(skillsCanvas, skillsPanel);
        SetPanelVisible(questCanvas, questPanel, false);
        SetPanelVisible(statsCanvas, statsPanel, false);
        SetPanelVisible(skillsCanvas, skillsPanel, open);
        if (open) SkillTreeManager.GetOrCreate().RefreshAll();
    }

    public void ClosePanels()
    {
        SetPanelVisible(questCanvas, questPanel, false);
        SetPanelVisible(statsCanvas, statsPanel, false);
        SetPanelVisible(skillsCanvas, skillsPanel, false);
    }

    public void ConfigureQuestPanel()
    {
        if (questCanvas == null)
            foreach (Canvas candidate in GetComponentsInChildren<Canvas>(true))
                if (candidate.name == "QuestCanvas") { questCanvas = candidate; break; }
        if (questCanvas == null) return;
        if (questPanel == null) questPanel = questCanvas.transform.Find("QuestPanel")?.gameObject;
        QuestJournalUI journal = questCanvas.GetComponent<QuestJournalUI>();
        if (journal == null) journal = questCanvas.gameObject.AddComponent<QuestJournalUI>();
        journal.BuildMissingLayout();
    }

    public void ToggleQuests()
    {
        if (SettingsUI.BlocksGameplay || TownQuestBoardUI.BlocksInput) return;
        ConfigureQuestPanel();
        bool open = !IsPanelOpen(questCanvas, questPanel);
        SetPanelVisible(statsCanvas, statsPanel, false);
        SetPanelVisible(skillsCanvas, skillsPanel, false);
        SetPanelVisible(questCanvas, questPanel, open);
        if (open) questCanvas.GetComponent<QuestJournalUI>().Refresh();
    }

    private static bool IsPanelOpen(Canvas canvas, GameObject panel)
    {
        return canvas != null && canvas.enabled && canvas.gameObject.activeInHierarchy
            && panel != null && panel.activeInHierarchy;
    }

    private void SetPanelVisible(Canvas canvas, GameObject panel, bool visible)
    {
        if (canvas == null || panel == null) return;
        // The decorative frame has a large transparent margin. Its full
        // rectangle must not intercept clicks intended for the side menu.
        Image frame = panel.GetComponent<Image>();
        if (frame != null) frame.raycastTarget = false;
        // Both the canvas and its panel may be disabled in edit mode.
        canvas.gameObject.SetActive(visible);
        panel.SetActive(visible);
        if (visible) canvas.enabled = true;
        SetGroupState(canvas.GetComponent<CanvasGroup>(), visible);
        SetGroupState(panel.GetComponent<CanvasGroup>(), visible);
        if (visible) KeepMenuAbovePanels();
    }

    private void KeepMenuAbovePanels()
    {
        if (menuCanvas == null) return;
        // These are separate canvases: sibling order alone does not guarantee
        // raycast priority after a canvas has been re-enabled.
        int panelOrder = Mathf.Max(
            statsCanvas != null ? statsCanvas.sortingOrder : 0,
            skillsCanvas != null ? skillsCanvas.sortingOrder : 0);
        panelOrder = Mathf.Max(panelOrder, questCanvas != null ? questCanvas.sortingOrder : 0);
        menuCanvas.sortingOrder = Mathf.Max(menuCanvas.sortingOrder, panelOrder + 1);
        menuCanvas.transform.SetAsLastSibling();
    }

    private static void SetGroupState(CanvasGroup group, bool visible)
    {
        if (group == null) return;
        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
    }
}
