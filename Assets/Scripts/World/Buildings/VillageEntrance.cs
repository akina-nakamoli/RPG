using UnityEngine;

[DisallowMultipleComponent]
public class VillageEntrance : MonoBehaviour
{
    [Tooltip("Gate approach rectangle, in the building's local coordinates.")]
    [SerializeField] private Vector2 regionCenter = new Vector2(0f, -0.8f);
    [SerializeField] private Vector2 regionSize = new Vector2(0.5f, 0.5f);
    [Tooltip("Player pivot destination, relative to the building. Optional marker overrides this.")]
    [SerializeField] private Vector2 arrivalLocalPosition = new Vector2(0f, 0.8f);
    [Tooltip("The point outside the gate where Yes returns the player.")]
    [SerializeField] private Vector2 returnLocalPosition = new Vector2(0f, -1.6f);
    [SerializeField] private Transform arrivalPoint;
    private PlayerMovements player;
    private VillageEntryPrompt prompt;
    private bool wasInsideEntry;
    private bool wasInsideExit;
    private float nextSearch;

    private void Update()
    {
        if (SettingsUI.BlocksGameplay) return;
        if ((player == null || prompt == null) && Time.unscaledTime >= nextSearch)
        {
            nextSearch = Time.unscaledTime + 1f;
            if (player == null) player = FindObjectOfType<PlayerMovements>();
            if (prompt == null) prompt = FindObjectOfType<VillageEntryPrompt>(true);
        }
        if (player == null) { wasInsideEntry = wasInsideExit = false; return; }
        Vector2 local = transform.InverseTransformPoint(player.transform.position);
        bool insideEntry = IsInside(local, regionCenter, wasInsideEntry);
        bool insideExit = IsInside(local, arrivalPoint != null
            ? (Vector2)transform.InverseTransformPoint(arrivalPoint.position) : arrivalLocalPosition, wasInsideExit);

        if (!insideEntry)
        {
            wasInsideEntry = false;
        }
        if (!insideExit) wasInsideExit = false;

        // No/Yes does not rearm either prompt. The player must leave that zone
        // and enter it again before it can appear again.
        if (insideEntry && !wasInsideEntry)
        {
            if (prompt != null && prompt.TryOpen(this, player, true))
                wasInsideEntry = true;
        }
        if (insideExit && !wasInsideExit)
        {
            if (prompt != null && prompt.TryOpen(this, player, false))
                wasInsideExit = true;
        }
    }

    private bool IsInside(Vector2 local, Vector2 center, bool wasInside)
    {
        Vector2 delta = local - center;
        float margin = wasInside ? 0.025f : 0f;
        return Mathf.Abs(delta.x) <= regionSize.x * 0.5f + margin
            && Mathf.Abs(delta.y) <= regionSize.y * 0.5f + margin;
    }

    public void ConfirmTravel(PlayerMovements visitor, bool entering)
    {
        Vector3 target = entering
            ? (arrivalPoint != null ? arrivalPoint.position : transform.TransformPoint(arrivalLocalPosition))
            : transform.TransformPoint(returnLocalPosition);
        visitor.TeleportTo(target);

        // The destination can be inside the opposite zone. Latch both zones
        // until the player walks out, preventing a prompt immediately on arrival.
        wasInsideEntry = true;
        wasInsideExit = true;
    }

    private void OnDisable()
    {
        if (prompt != null) prompt.Cancel(this);
        wasInsideEntry = wasInsideExit = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(regionCenter, regionSize);
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireCube(arrivalPoint != null
            ? (Vector2)transform.InverseTransformPoint(arrivalPoint.position) : arrivalLocalPosition, regionSize);
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color = Color.green;
        Vector3 target = arrivalPoint != null ? arrivalPoint.position
            : transform.TransformPoint(arrivalLocalPosition);
        Gizmos.DrawWireSphere(target, 0.25f);
    }
}
