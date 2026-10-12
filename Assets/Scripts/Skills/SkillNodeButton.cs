using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class SkillNodeButton : MonoBehaviour
{
    [SerializeField] private int skillColumn = -1;
    [SerializeField] private int skillRow = -1;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private Sprite lockedSprite;

    private SkillTreeManager manager;
    private Image buttonImage;
    private Image iconImage;
    private Image lockImage;
    private bool visualsInitialized;
    private Color normalButtonColor = Color.white;
    private Color normalIconColor = Color.white;
    private Color normalTextColor = Color.white;

    private static readonly Color LockedTint = new Color(0.38f, 0.38f, 0.38f, 1f);

    public int SkillColumn => skillColumn;
    public int SkillRow => skillRow;

    private void Awake()
    {
        InitializeVisuals();
        TryReadNodeName();

        manager = SkillTreeManager.GetOrCreate();
        upgradeButton.onClick.AddListener(OnClicked);
        manager.RegisterNode(this);
    }

    private void InitializeVisuals()
    {
        // The manager can refresh nodes while their panel is inactive,
        // before Awake runs. Capture the original colors only once.
        if (visualsInitialized) return;
        upgradeButton = upgradeButton != null ? upgradeButton : GetComponent<Button>();
        // Column headings are also children of first-row buttons. Only bind
        // the rank label inside the icon, never the first TMP text we find.
        Transform rankLabel = transform.Find("SkillsIcon/LevelText");
        levelText = rankLabel != null ? rankLabel.GetComponent<TMP_Text>() : levelText;
        buttonImage = GetComponent<Image>();
        Transform skillIcon = transform.Find("SkillsIcon");
        iconImage = skillIcon != null ? skillIcon.GetComponent<Image>() : null;
        Image[] images = GetComponentsInChildren<Image>(true);
        foreach (Image image in images)
        {
            if (iconImage == null && image != buttonImage && image.name != "LockIcon")
            {
                iconImage = image;
                break;
            }
        }

        if (buttonImage != null)
            normalButtonColor = buttonImage.color;
        if (iconImage != null)
            normalIconColor = iconImage.color;
        if (levelText != null)
            normalTextColor = levelText.color;

        if (lockedSprite != null)
        {
            Transform parent = iconImage != null ? iconImage.transform : transform;
            GameObject overlay = new GameObject("LockIcon", typeof(RectTransform), typeof(Image));
            overlay.layer = gameObject.layer;
            overlay.transform.SetParent(parent, false);
            lockImage = overlay.GetComponent<Image>();
            lockImage.sprite = lockedSprite;
            lockImage.preserveAspect = true;
            lockImage.raycastTarget = false;
            // Draw above the skill artwork, but below its rank label.
            overlay.transform.SetAsFirstSibling();
            RectTransform rect = lockImage.rectTransform;
            rect.anchorMin = new Vector2(0f, 0.08f);
            rect.anchorMax = new Vector2(1f, 1.08f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            overlay.SetActive(false);
        }

        visualsInitialized = true;
    }

    private void OnDestroy()
    {
        if (upgradeButton != null)
            upgradeButton.onClick.RemoveListener(OnClicked);
        if (manager != null)
            manager.UnregisterNode(this);
    }

    private void TryReadNodeName()
    {
        if (skillColumn >= 0 && skillRow >= 0)
            return;

        if (!TryParseNodeName(gameObject.name, out int parsedColumn, out int parsedRow))
        {
            Debug.LogWarning("技能按钮名称应为 SkillsButton1_1 到 SkillsButton5_3：" + gameObject.name, this);
            return;
        }

        skillColumn = parsedColumn;
        skillRow = parsedRow;
    }

    public static bool TryParseNodeName(string objectName, out int column, out int row)
    {
        Match match = Regex.Match(objectName ?? string.Empty, @"SkillsButton([1-5])_([1-3])");
        if (!match.Success)
        {
            column = -1;
            row = -1;
            return false;
        }

        column = int.Parse(match.Groups[1].Value) - 1;
        row = int.Parse(match.Groups[2].Value) - 1;
        return true;
    }

    private void OnClicked() => manager.Upgrade(this);

    public void Refresh(int level, bool isUnlocked, bool canUpgrade)
    {
        InitializeVisuals();
        if (levelText != null)
        {
            levelText.text = level + "/" + StatsManager.SkillRanksPerNode;
            levelText.color = isUnlocked ? normalTextColor : normalTextColor * LockedTint;
        }

        if (upgradeButton != null)
            upgradeButton.interactable = canUpgrade;

        // Keep unlocked nodes bright even when the player has no points.
        // Only nodes whose prerequisite is incomplete receive the dark tint.
        if (buttonImage != null)
            buttonImage.color = isUnlocked ? normalButtonColor : normalButtonColor * LockedTint;
        if (iconImage != null)
            iconImage.color = isUnlocked ? normalIconColor : normalIconColor * LockedTint;
        if (lockImage != null)
            lockImage.gameObject.SetActive(!isUnlocked);
    }
}
