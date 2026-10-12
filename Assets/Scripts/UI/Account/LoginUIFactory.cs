using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared pixel-art UI construction; editor-generated children remain editable.</summary>
public sealed class LoginUIFactory
{
    private readonly TMP_FontAsset font;
    private readonly Sprite paper, button;
    private static readonly Color Ink = new Color(0.12f, 0.17f, 0.21f);
    public LoginUIFactory(TMP_FontAsset textFont, Sprite paperSprite, Sprite buttonSprite)
    {
        font = textFont != null ? textFont : Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        paper = paperSprite; button = buttonSprite;
    }

    public RectTransform Canvas(string name, int sortingOrder)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        obj.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        obj.GetComponent<Canvas>().sortingOrder = sortingOrder;
        CanvasScaler scaler = obj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
        return (RectTransform)obj.transform;
    }

    public RectTransform Panel(Transform parent, string name, Vector2 position, Vector2 size)
    {
        RectTransform rect = Rect(parent, name, position, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = paper; image.color = paper != null ? Color.white : new Color(0.94f, 0.89f, 0.72f);
        return rect;
    }

    public static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position; rect.sizeDelta = size;
        return rect;
    }

    public TMP_Text Text(Transform parent, string name, string value, Vector2 position, Vector2 size, float fontSize)
    {
        var text = Rect(parent, name, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.text = value; text.fontSize = fontSize; text.color = Ink;
        text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        text.richText = false;
        return text;
    }

    public Button Button(Transform parent, string name, string label, Vector2 position, Vector2 size)
    {
        RectTransform rect = Rect(parent, name, position, size);
        Image image = rect.gameObject.AddComponent<Image>(); image.sprite = button;
        image.color = button != null ? Color.white : new Color(0.50f, 0.72f, 0.74f);
        Button result = rect.gameObject.AddComponent<Button>(); result.targetGraphic = image;
        ColorBlock colors = result.colors; colors.highlightedColor = new Color(1f, 0.95f, 0.78f);
        colors.pressedColor = new Color(0.7f, 0.8f, 0.8f); result.colors = colors;
        Text(rect, "Label", label, Vector2.zero, size - new Vector2(24, 12), 24);
        return result;
    }

    public TMP_InputField Input(Transform parent, string name, string placeholder, Vector2 position, bool password)
    {
        RectTransform rect = Panel(parent, name, position, new Vector2(600, 66));
        Image image = rect.GetComponent<Image>(); image.sprite = null; image.color = new Color(1f, 0.98f, 0.89f);
        RectTransform viewport = Rect(rect, "TextArea", Vector2.zero, new Vector2(560, 58));
        viewport.gameObject.AddComponent<RectMask2D>();
        TMP_Text text = Text(viewport, "Text", "", Vector2.zero, new Vector2(552, 56), 27);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        TMP_Text hint = Text(viewport, "Placeholder", placeholder, Vector2.zero, new Vector2(552, 56), 24);
        hint.alignment = TextAlignmentOptions.MidlineLeft; hint.color = new Color(0.4f, 0.4f, 0.4f, 0.8f);
        TMP_InputField field = rect.gameObject.AddComponent<TMP_InputField>();
        field.targetGraphic = image; field.textViewport = viewport;
        field.textComponent = (TextMeshProUGUI)text; field.placeholder = hint;
        field.contentType = password ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
        field.characterLimit = password ? 128 : 20;
        field.lineType = TMP_InputField.LineType.SingleLine;
        return field;
    }
}
