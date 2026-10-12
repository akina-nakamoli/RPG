using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Editable prefab references. Gameplay never rebuilds or restyles these controls.</summary>
public class TargetRangePanelUI : MonoBehaviour
{
    public GameObject panel;
    public GameObject backdrop;
    public TMP_Text status;
    public TMP_Text rules;
    public RectTransform arena;
    public Button emptyShot;
    public Button target;
    public TMP_Text targetLabel;
    public Button start;
    public TMP_Text startLabel;
    public Button exit;
    [Header("Random targets (relative to Target's authored size)")]
    [Min(.1f)] public float minimumTargetScale = .65f;
    [Min(.1f)] public float maximumTargetScale = 1.15f;
    [Min(0f)] public float edgePadding = 18f;
    [Tooltip("Movement amplitude in arena pixels during the final 10 seconds.")]
    public Vector2 movementAmplitude = new Vector2(30f, 15f);
    private void Awake()
    {
        // Visible in prefab editing, hidden when the game starts.
        if (!Application.isPlaying) return;
        if (panel != null) panel.SetActive(false);
        if (backdrop != null) backdrop.SetActive(false);
        if (target != null) target.gameObject.SetActive(false);
    }
}
