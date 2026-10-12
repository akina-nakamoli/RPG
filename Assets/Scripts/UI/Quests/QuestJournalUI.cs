using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Read-only progress journal inside the user's existing QuestCanvas.</summary>
public class QuestJournalUI : MonoBehaviour
{
    [SerializeField] private GameObject[] rows;
    [SerializeField] private TMP_Text[] titles, objectives, statuses;
    [SerializeField] private TMP_Text emptyText, pageText;
    [SerializeField] private Button previousButton, nextButton, closeButton;
    private TownQuestBoardUI board;
    private readonly List<TownQuestBoardUI.QuestOffer> entries = new List<TownQuestBoardUI.QuestOffer>();
    private int page;
    private bool listenersBound;
    [SerializeField] private int appearanceVersion;
    public bool NeedsAppearanceUpdate => appearanceVersion < 1;

    private void OnEnable()
    {
        BuildMissingLayout();
        ApplyTownAppearance();
        if (rows == null || rows.Length == 0) return;
        if (!listenersBound)
        {
            previousButton.onClick.AddListener(Previous);
            nextButton.onClick.AddListener(Next);
            closeButton.onClick.AddListener(Close);
            listenersBound = true;
        }
        board = TownQuestBoardUI.FindOrCreate();
        if (board != null) board.Changed += Refresh;
        Refresh();
    }
    private void OnDisable()
    {
        if (board != null) board.Changed -= Refresh;
    }
    private void Previous() { page--; Refresh(); }
    private void Next() { page++; Refresh(); }
    private void Close()
    {
        GameMenuUI menu = FindObjectOfType<GameMenuUI>();
        if (menu != null) menu.ClosePanels();
        else gameObject.SetActive(false);
    }

    public void Refresh()
    {
        if (rows == null || rows.Length == 0) return;
        entries.Clear();
        if (board != null)
        {
            var ids = new HashSet<string>();
            foreach (TownQuestBoardUI.QuestOffer offer in board.Offers)
                if (offer != null && ids.Add(offer.id) && board.IsAccepted(offer.id)) entries.Add(offer);
        }
        page = Mathf.Clamp(page, 0, Mathf.Max(0, (entries.Count - 1) / rows.Length));
        emptyText.gameObject.SetActive(entries.Count == 0);
        for (int i = 0; i < rows.Length; i++)
        {
            int index = page * rows.Length + i;
            rows[i].SetActive(index < entries.Count);
            Transform divider = rows[i].transform.Find("TaskDivider");
            if (divider != null) divider.gameObject.SetActive(index + 1 < entries.Count && i < rows.Length - 1);
            if (index >= entries.Count) continue;
            var quest = entries[index];
            titles[i].text = quest.title;
            objectives[i].text = quest.objective + "  (" + board.ProgressLabel(quest.id) + ")";
            bool completed = board.IsCompleted(quest.id), ready = board.CanTurnIn(quest.id);
            statuses[i].text = board.QuestStatus(quest.id);
            statuses[i].color = completed ? new Color(0.14f, 0.40f, 0.20f)
                : ready ? new Color(0.53f, 0.28f, 0.02f) : new Color(0.28f, 0.30f, 0.32f);
        }
        previousButton.interactable = page > 0;
        nextButton.interactable = (page + 1) * rows.Length < entries.Count;
        pageText.text = (page + 1) + " / " + Mathf.Max(1, Mathf.CeilToInt((float)entries.Count / rows.Length));
    }

    [ContextMenu("Match Town Quest Appearance")]
    public void ApplyTownAppearance()
    {
        if (rows == null || rows.Length == 0) return;
        TownQuestBoardUI source = FindObjectOfType<TownQuestBoardUI>(true);
#if UNITY_EDITOR
        if (source == null && !Application.isPlaying)
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/TownQuestCanvas.prefab");
            if (prefab != null) source = prefab.GetComponent<TownQuestBoardUI>();
        }
#endif
        if (source == null) source = TownQuestBoardUI.FindOrCreate();
        if (source == null) return;
        CopyButtonStyle(previousButton, source.transform, "PreviousButton");
        CopyButtonStyle(nextButton, source.transform, "NextButton");
        CopyButtonStyle(closeButton, source.transform, "CloseButton");
        if (closeButton != null)
        {
            var rect = (RectTransform)closeButton.transform;
            rect.anchoredPosition = new Vector2(260, -365); rect.sizeDelta = new Vector2(180, 60);
        }
        foreach (var button in new[] { previousButton, nextButton })
            if (button != null) ((RectTransform)button.transform).sizeDelta = new Vector2(70, 55);
        TMP_Text sourcePage = null;
        foreach (var text in source.GetComponentsInChildren<TMP_Text>(true)) if (text.name == "PageText") sourcePage = text;
        if (pageText != null && sourcePage != null) { pageText.font = sourcePage.font; pageText.fontStyle = sourcePage.fontStyle; pageText.color = sourcePage.color; }
        for (int i = 0; i < rows.Length; i++)
        {
            Image background = rows[i].GetComponent<Image>();
            if (background != null) { background.color = Color.clear; background.raycastTarget = false; }
            if (i == rows.Length - 1) continue;
            Transform existing = rows[i].transform.Find("TaskDivider");
            RectTransform rect = existing != null ? (RectTransform)existing : LoginUIFactory.Rect(rows[i].transform, "TaskDivider", new Vector2(0, -80), new Vector2(630, 3));
            Image line = rect.GetComponent<Image>(); if (line == null) line = rect.gameObject.AddComponent<Image>();
            line.color = new Color(.44f, .34f, .21f, .35f); line.raycastTarget = false;
        }
        if (!Application.isPlaying && emptyText != null) emptyText.gameObject.SetActive(false);
        appearanceVersion = 1;
    }
    private static void CopyButtonStyle(Button target, Transform sourceRoot, string sourceName)
    {
        if (target == null) return;
        Button source = null;
        foreach (Button candidate in sourceRoot.GetComponentsInChildren<Button>(true)) if (candidate.name == sourceName) { source = candidate; break; }
        if (source == null) return;
        var from = source.GetComponent<Image>(); var to = target.GetComponent<Image>();
        if (from != null && to != null)
        {
            to.sprite = from.sprite; to.type = from.type; to.color = from.color;
            to.pixelsPerUnitMultiplier = from.pixelsPerUnitMultiplier;
            to.preserveAspect = from.preserveAspect; target.targetGraphic = to;
        }
        target.colors = source.colors; target.transition = source.transition;
        var label = target.GetComponentInChildren<TMP_Text>(true); var reference = source.GetComponentInChildren<TMP_Text>(true);
        if (label != null && reference != null)
        {
            label.font = reference.font; label.fontSharedMaterial = reference.fontSharedMaterial;
            label.fontStyle = reference.fontStyle; label.color = reference.color;
            label.enableAutoSizing = true; label.fontSizeMin = 16; label.fontSizeMax = reference.fontSize;
            var rect = (RectTransform)label.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(8, 5); rect.offsetMax = new Vector2(-8, -5);
        }
    }

    [ContextMenu("Build Missing Quest Entries")]
    public void BuildMissingLayout()
    {
        if (rows != null && rows.Length > 0) return;
        Transform panel = transform.Find("QuestPanel");
        Transform content = panel != null ? panel.Find("QuestContnet") : null;
        if (content == null && panel != null) content = panel.Find("QuestContent");
        if (content == null) { Debug.LogWarning("QuestCanvas requires QuestPanel/QuestContnet (or QuestContent).", this); return; }
        // Preserve the user's parchment, banner, transform and font choices.
        TMP_Text heading = panel.GetComponentInChildren<TMP_Text>(true);
        var ui = new LoginUIFactory(heading != null ? heading.font : null, null, null);
        RectTransform root = LoginUIFactory.Rect(content, "QuestEntries", Vector2.zero, new Vector2(700, 500));
        rows = new GameObject[3]; titles = new TMP_Text[3]; objectives = new TMP_Text[3]; statuses = new TMP_Text[3];
        for (int i = 0; i < rows.Length; i++)
        {
            RectTransform row = ui.Panel(root, "QuestRow" + (i + 1), new Vector2(0, 160 - i * 160), new Vector2(660, 144));
            row.GetComponent<Image>().color = new Color(1f, 0.98f, 0.86f, 0.55f);
            rows[i] = row.gameObject;
            titles[i] = ui.Text(row, "Title", "Quest title", new Vector2(0, 43), new Vector2(620, 40), 30);
            titles[i].fontStyle = FontStyles.Bold;
            objectives[i] = ui.Text(row, "Objective", "Objective  (0 / 3)", Vector2.zero, new Vector2(620, 47), 24);
            statuses[i] = ui.Text(row, "Status", "IN PROGRESS", new Vector2(0, -44), new Vector2(620, 36), 23);
        }
        emptyText = ui.Text(root, "EmptyText", "No accepted quests.\nVisit the Task Hall to accept a task.", Vector2.zero, new Vector2(630, 180), 28);
        emptyText.gameObject.SetActive(false);
        previousButton = ui.Button(panel, "JournalPrevious", "<", new Vector2(-235, -295), new Vector2(90, 55));
        nextButton = ui.Button(panel, "JournalNext", ">", new Vector2(235, -295), new Vector2(90, 55));
        pageText = ui.Text(panel, "JournalPage", "1 / 1", new Vector2(0, -295), new Vector2(220, 50), 24);
        closeButton = ui.Button(panel, "JournalClose", "CLOSE", new Vector2(0, -365), new Vector2(270, 60));
        ui.Text(panel, "TurnInHint", "Return to the Task Hall to turn in ready quests.", new Vector2(0, -430), new Vector2(850, 45), 22);
        // Opaque content catches clicks, decorative frame must not cover the HUD menu.
        Image frame = panel.GetComponent<Image>();
        if (frame != null) frame.raycastTarget = false;
    }
}
