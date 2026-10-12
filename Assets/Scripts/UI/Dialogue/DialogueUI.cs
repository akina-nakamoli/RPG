using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Displays dialogue in the existing DialogueCanvas and releases player control at the end.</summary>
public class DialogueUI : MonoBehaviour
{
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TMP_Text speakerText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Image characterIcon;
    [SerializeField] private KeyCode advanceKey = KeyCode.E;
    [SerializeField] private string playerName = "russ";
    [Header("Nameplate Font (does not change dialogue text)")]
    [Tooltip("Font used only for russ/niko/jame in the nameplate. Dialogue body keeps its own font.")]
    [SerializeField] private TMP_FontAsset speakerFont;

    private PlayerMovements player;
    private NPCDialogue.DialogueLine[] lines;
    private string defaultNpcName;
    private Sprite npcPortrait;
    private Sprite playerPortrait;
    private int lineIndex;
    private Button clickToAdvance;
    private static TMP_FontAsset runtimeCjkFont;

    public bool IsPlaying { get; private set; }
    public event System.Action DialogueCompleted;

    private void Awake()
    {
        ResolveOrCreateUI();
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
    }
    public static void ApplyReadableChinese(TMP_Text text)
    {
        if (text == null || !ContainsCjk(text.text)) return;
        var font = GetRuntimeCjkFont();
        if (font == null) return;
        font.TryAddCharacters(text.text); text.font = font; text.fontSharedMaterial = font.material;
        text.fontStyle = FontStyles.Bold;
    }

    public bool Begin(PlayerMovements visitor, string npcName, NPCDialogue.DialogueLine[] dialogue,
        SpriteRenderer npcRenderer)
    {
        if (visitor == null || dialogue == null || dialogue.Length == 0 || IsPlaying) return false;
        // The canvas is intentionally inactive in edit mode. Activate it before
        // resolving UI references so Awake can initialize the panel and controls.
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (!gameObject.activeInHierarchy || !enabled) return false;
        ResolveOrCreateUI();
        if (dialoguePanel == null || speakerText == null || messageText == null) return false;

        player = visitor;
        lines = dialogue;
        defaultNpcName = npcName;
        npcPortrait = npcRenderer != null ? npcRenderer.sprite : null;
        SpriteRenderer playerRenderer = player.GetComponentInChildren<SpriteRenderer>();
        playerPortrait = playerRenderer != null ? playerRenderer.sprite : null;
        lineIndex = 0;
        IsPlaying = true;
        player.SetInteractionLocked(true);
        dialoguePanel.SetActive(true);
        dialoguePanel.transform.SetAsLastSibling();
        ShowLine();
        return true;
    }

    private void Update()
    {
        if (SettingsUI.BlocksGameplay) return;
        if (!IsPlaying) return;
        if (Input.GetKeyDown(advanceKey) || Input.GetKeyDown(KeyCode.Space)) Advance();
    }

    public void Advance()
    {
        if (!IsPlaying) return;
        lineIndex++;
        if (lines == null || lineIndex >= lines.Length)
        {
            End(true);
            return;
        }
        ShowLine();
    }

    private void ShowLine()
    {
        NPCDialogue.DialogueLine line = lines[lineIndex];
        bool isPlayer = line.playerSpeaking;
        // NPC lines always use the name configured on this NPC's component;
        // per-line speaker labels remain available for player lines.
        speakerText.text = isPlayer
            ? playerName
            : (string.IsNullOrWhiteSpace(defaultNpcName) ? "NPC" : defaultNpcName);
        messageText.text = (line.text ?? string.Empty)
            + "\n\n<color=#746B5D><size=70%>按 E / 空格继续，或点击对话框</size></color>";
        if (ContainsCjk(speakerText.text) || ContainsCjk(messageText.text))
        {
            TMP_FontAsset cjkFont = GetRuntimeCjkFont();
            if (cjkFont != null)
            {
                messageText.font = cjkFont;
                messageText.fontSharedMaterial = cjkFont.material;
                cjkFont.TryAddCharacters(speakerText.text + messageText.text);
            }
        }
        speakerText.color = new Color(.06f, .10f, .10f, 1f);
        messageText.color = new Color(.07f, .09f, .09f, 1f);
        // An Inspector override controls the nameplate independently of the body.
        TMP_FontAsset nameFont = ContainsCjk(speakerText.text) ? GetRuntimeCjkFont()
            : (speakerFont != null ? speakerFont : Resources.Load<TMP_FontAsset>("Fonts & Materials/Bangers SDF"));
        if (nameFont != null)
        {
            speakerText.font = nameFont;
            speakerText.fontSharedMaterial = nameFont.material;
        }
        speakerText.fontStyle = FontStyles.Normal;
        speakerText.characterSpacing = 0f;
        messageText.fontStyle = FontStyles.Bold;
        messageText.enableWordWrapping = true;
        messageText.enableAutoSizing = true;
        messageText.fontSizeMin = 26f;
        messageText.fontSizeMax = 38f;
        messageText.overflowMode = TextOverflowModes.Overflow;
        speakerText.ForceMeshUpdate();
        messageText.ForceMeshUpdate();
        if (characterIcon != null)
        {
            characterIcon.sprite = isPlayer ? playerPortrait : npcPortrait;
            characterIcon.enabled = characterIcon.sprite != null;
        }
    }

    private void End(bool completed)
    {
        if (!IsPlaying) return;
        IsPlaying = false;
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        if (player != null) player.SetInteractionLocked(false);
        player = null;
        lines = null;
        if (completed)
        {
            DialogueCompleted?.Invoke();
            gameObject.SetActive(false);
        }
    }

    private void ResolveOrCreateUI()
    {
        Transform[] descendants = GetComponentsInChildren<Transform>(true);
        Transform panelTransform = FindByName(descendants, "DialoguePanel");
        if (dialoguePanel == null && panelTransform != null) dialoguePanel = panelTransform.gameObject;
        if (dialoguePanel == null) dialoguePanel = CreateFallbackPanel().gameObject;

        if (speakerText == null)
        {
            Transform nameRoot = FindByName(descendants, "CharacterName");
            speakerText = FindText(nameRoot);
            if (speakerText == null)
                speakerText = CreateText(nameRoot != null ? nameRoot : dialoguePanel.transform, "SpeakerText", true);
        }
        if (messageText == null)
        {
            Transform messageRoot = FindByName(descendants, "MessageBG");
            messageText = FindText(messageRoot);
            if (messageText == null)
            {
                Transform parent = messageRoot != null ? messageRoot : dialoguePanel.transform;
                messageText = CreateText(parent, "DialogueText", false);
            }
        }
        if (characterIcon == null)
        {
            Transform iconRoot = FindByName(descendants, "CharacterIcon");
            if (iconRoot != null)
            {
                Transform portrait = FindByName(iconRoot.GetComponentsInChildren<Transform>(true), "Image");
                characterIcon = portrait != null ? portrait.GetComponent<Image>() : iconRoot.GetComponentInChildren<Image>(true);
            }
        }

        if (clickToAdvance == null)
        {
            clickToAdvance = dialoguePanel.GetComponent<Button>();
            if (clickToAdvance == null) clickToAdvance = dialoguePanel.AddComponent<Button>();
            clickToAdvance.transition = Selectable.Transition.None;
            clickToAdvance.onClick.RemoveListener(Advance);
            clickToAdvance.onClick.AddListener(Advance);
        }
    }

    private RectTransform CreateFallbackPanel()
    {
        GameObject panel = new GameObject("DialoguePanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(transform, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 90f);
        rect.sizeDelta = new Vector2(1200f, 280f);
        panel.GetComponent<Image>().color = new Color(0.12f, 0.1f, 0.08f, 0.94f);
        return rect;
    }

    private TMP_Text CreateText(Transform parent, string objectName, bool isSpeaker)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = isSpeaker ? new Vector2(0.06f, 0.68f) : new Vector2(0.06f, 0.12f);
        rect.anchorMax = isSpeaker ? new Vector2(0.94f, 0.94f) : new Vector2(0.94f, 0.64f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = isSpeaker ? 36f : 32f;
        text.color = isSpeaker ? new Color(1f, 0.84f, 0.45f) : Color.white;
        text.alignment = isSpeaker ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.TopLeft;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        return text;
    }

    private static bool ContainsCjk(string value)
    {
        foreach (char character in value)
            if (character >= 0x2E80 && character <= 0x9FFF) return true;
        return false;
    }

    private static TMP_FontAsset GetRuntimeCjkFont()
    {
        if (runtimeCjkFont != null) return runtimeCjkFont;
        Font systemFont = Resources.Load<Font>("Fonts/NotoSansSC");
        if (systemFont == null) systemFont = Font.CreateDynamicFontFromOSFont(
            new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei" }, 64);
        if (systemFont == null) return TMP_Settings.defaultFontAsset;
        runtimeCjkFont = TMP_FontAsset.CreateFontAsset(systemFont, 64, 9,
            UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
        if (runtimeCjkFont != null)
        {
            runtimeCjkFont.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            // The source font's default face is thin; give TMP's synthesized bold a clear weight.
            runtimeCjkFont.boldStyle = 1.2f;
        }
        return runtimeCjkFont;
    }

    private static Transform FindByName(Transform[] objects, string objectName)
    {
        foreach (Transform item in objects)
            if (item.name == objectName) return item;
        return null;
    }

    private static TMP_Text FindText(Transform root)
    {
        return root != null ? root.GetComponentInChildren<TMP_Text>(true) : null;
    }

    private void OnDisable()
    {
        if (clickToAdvance != null) clickToAdvance.onClick.RemoveListener(Advance);
        if (IsPlaying) End(false);
    }
}
