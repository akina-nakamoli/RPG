using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Connects the skill-tree buttons to StatsManager and refreshes their UI.
/// It can create itself automatically, so no scene reference is required.
/// </summary>
public class SkillTreeManager : MonoBehaviour
{
    public static SkillTreeManager Instance { get; private set; }

    [SerializeField] private TMP_Text skillPointsText;
    private readonly Dictionary<SkillNodeButton, bool> nodes = new Dictionary<SkillNodeButton, bool>();
    private StatsManager stats;
    private string skillPointsPrefix = "SkillsPoints:";
    private bool skillPointsPrefixCaptured;

    public static SkillTreeManager GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        Instance = FindObjectOfType<SkillTreeManager>();
        if (Instance != null)
            return Instance;

        GameObject managerObject = new GameObject("SkillTreeManager");
        Instance = managerObject.AddComponent<SkillTreeManager>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        stats = StatsManager.GetOrCreate();
        stats.SkillTreeChanged += RefreshAll;
        FindSkillPointsText();
    }

    private void Start() => RefreshAll();

    private void OnDestroy()
    {
        if (stats != null)
            stats.SkillTreeChanged -= RefreshAll;
        if (Instance == this)
            Instance = null;
    }

    public void RegisterNode(SkillNodeButton node)
    {
        if (node == null) return;
        nodes[node] = true;
        RefreshNode(node);
    }

    public void UnregisterNode(SkillNodeButton node)
    {
        if (node != null)
            nodes.Remove(node);
    }

    public void Upgrade(SkillNodeButton node)
    {
        if (node == null || stats == null) return;
        if (stats.TryUpgradeSkill(node.SkillColumn, node.SkillRow))
            RefreshAll();
    }

    public bool CanUpgrade(SkillNodeButton node)
    {
        if (node == null || stats == null) return false;
        return stats.skillPoints > 0
            && stats.IsSkillNodeUnlocked(node.SkillColumn, node.SkillRow)
            && stats.GetSkillLevel(node.SkillColumn, node.SkillRow) < StatsManager.SkillRanksPerNode;
    }

    public void RefreshAll()
    {
        if (stats == null)
            stats = StatsManager.GetOrCreate();

        FindSkillPointsText();
        SkillNodeButton[] sceneNodes = FindObjectsOfType<SkillNodeButton>(true);
        foreach (SkillNodeButton node in sceneNodes)
            RegisterNode(node);

        // Runtime fallback for scene instances that were created before the
        // SkillNodeButton component was added to the prefab.
        Button[] buttons = FindObjectsOfType<Button>(true);
        foreach (Button button in buttons)
        {
            if (button == null || !SkillNodeButton.TryParseNodeName(button.gameObject.name, out _, out _))
                continue;

            SkillNodeButton node = button.GetComponent<SkillNodeButton>();
            if (node == null)
                node = button.gameObject.AddComponent<SkillNodeButton>();
            RegisterNode(node);
        }

        List<SkillNodeButton> stale = new List<SkillNodeButton>();
        foreach (SkillNodeButton node in nodes.Keys)
        {
            if (node == null)
                stale.Add(node);
            else
                RefreshNode(node);
        }

        foreach (SkillNodeButton node in stale)
            nodes.Remove(node);
    }

    private void RefreshNode(SkillNodeButton node)
    {
        if (node != null)
        {
            bool unlocked = stats.IsSkillNodeUnlocked(node.SkillColumn, node.SkillRow);
            node.Refresh(
                stats.GetSkillLevel(node.SkillColumn, node.SkillRow),
                unlocked,
                CanUpgrade(node));
        }
    }

    private void FindSkillPointsText()
    {
        if (skillPointsText == null)
        {
            SkillPointsTextMarker marker = FindObjectOfType<SkillPointsTextMarker>(true);
            if (marker != null)
                skillPointsText = marker.Text;
            else
            {
                TMP_Text[] texts = Resources.FindObjectsOfTypeAll<TMP_Text>();
                foreach (TMP_Text text in texts)
                {
                    if (text != null && text.gameObject.name == "SkillsPointsText" && text.gameObject.scene.IsValid())
                    {
                        skillPointsText = text;
                        break;
                    }
                }
            }
        }

        if (skillPointsText != null && !skillPointsPrefixCaptured)
        {
            string originalText = skillPointsText.text;
            int separatorIndex = originalText != null ? originalText.IndexOf(':') : -1;
            if (separatorIndex >= 0)
                skillPointsPrefix = originalText.Substring(0, separatorIndex + 1);
            skillPointsPrefixCaptured = true;
        }

        if (skillPointsText != null && stats != null)
            skillPointsText.text = skillPointsPrefix + stats.skillPoints;
    }
}

// Optional marker that can be added to a different text object if the name changes.
public class SkillPointsTextMarker : MonoBehaviour
{
    [SerializeField] private TMP_Text text;
    public TMP_Text Text => text != null ? text : GetComponent<TMP_Text>();
}
