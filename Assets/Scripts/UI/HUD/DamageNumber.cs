using TMPro;
using UnityEngine;

// A short-lived world-space effect; it survives the attacker/target being disabled.
public sealed class DamageNumber : MonoBehaviour
{
    private TextMeshPro label;
    private Vector3 origin;
    private Color tint;
    private float age;
    private const float Duration = 0.85f;

    public static void Show(Vector2 position, int damage, bool againstPlayer, bool critical = false,
        TMP_FontAsset font = null)
    {
        if (damage <= 0) return;
        var obj = new GameObject("DamageNumber", typeof(TextMeshPro), typeof(DamageNumber));
        var effect = obj.GetComponent<DamageNumber>();
        effect.origin = new Vector3(position.x, position.y, 0f);
        obj.transform.position = effect.origin;
        effect.tint = againstPlayer ? new Color(1f, 0.28f, 0.22f)
            : critical ? new Color(1f, 0.8f, 0.15f) : Color.white;
        effect.label = obj.GetComponent<TextMeshPro>();
        effect.label.font = font != null ? font : TMP_Settings.defaultFontAsset;
        effect.label.text = "-" + damage;
        effect.label.fontSize = critical ? 5.2f : 4.5f;
        effect.label.fontStyle = FontStyles.Bold;
        effect.label.alignment = TextAlignmentOptions.Center;
        effect.label.enableWordWrapping = false;
        effect.label.raycastTarget = false;
        effect.label.color = effect.tint;
        effect.label.outlineColor = new Color(0.08f, 0.1f, 0.14f);
        effect.label.outlineWidth = 0.15f;
        effect.label.rectTransform.sizeDelta = new Vector2(3f, 1f);
        effect.label.GetComponent<MeshRenderer>().sortingOrder = 32760;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float progress = Mathf.Clamp01(age / Duration);
        transform.position = origin + Vector3.up * (progress * 0.65f);
        transform.localScale = Vector3.one * Mathf.Lerp(1.2f, 1f, Mathf.Clamp01(progress * 5f));
        Color color = tint;
        color.a = 1f - Mathf.InverseLerp(0.35f, 1f, progress);
        label.color = color;
        if (age >= Duration) Destroy(gameObject);
    }
}
