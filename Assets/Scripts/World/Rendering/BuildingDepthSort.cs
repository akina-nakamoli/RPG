using UnityEngine;

/// <summary>Visual depth only. Building interaction and collision remain independent.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class BuildingDepthSort : MonoBehaviour
{
    [Header("Building Ground Point")]
    [Tooltip("Optional child placed at the doorway/ground. Otherwise uses the solid collider's bottom.")]
    [SerializeField] private Transform groundPoint;
    [Tooltip("Local-space adjustment to the ground point. Positive Y raises the sorting boundary.")]
    [SerializeField] private float groundOffsetY;
    private SpriteRenderer visual;
    private Vector3 localGround;
    private int originalOrder;

    private void Awake()
    {
        visual = GetComponent<SpriteRenderer>();
        originalOrder = visual.sortingOrder;
        // Cache a stable point so future animations do not shift the depth boundary.
        localGround = CalculateLocalGround();
    }

    private Vector3 CalculateLocalGround()
    {
        foreach (Collider2D collider in GetComponents<Collider2D>())
        {
            if (collider.isTrigger || !collider.enabled) continue;
            return transform.InverseTransformPoint(new Vector3(collider.bounds.center.x,
                collider.bounds.min.y, transform.position.z));
        }
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        return renderer != null ? transform.InverseTransformPoint(new Vector3(renderer.bounds.center.x,
            renderer.bounds.min.y, transform.position.z)) : Vector3.zero;
    }

    private Vector3 GroundPosition
    {
        get
        {
            Vector3 point = groundPoint != null ? groundPoint.position : transform.TransformPoint(localGround);
            return point + transform.TransformVector(new Vector3(0, groundOffsetY, 0));
        }
    }

    private void LateUpdate()
    {
        if (visual != null) visual.sortingOrder = WorldDepthSort.OrderAt(GroundPosition.y);
    }

    private void OnDisable()
    {
        if (visual != null) visual.sortingOrder = originalOrder;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 point = groundPoint != null ? groundPoint.position
            : transform.TransformPoint(Application.isPlaying ? localGround : CalculateLocalGround());
        point += transform.TransformVector(new Vector3(0, groundOffsetY, 0));
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(point, 0.08f);
        Gizmos.DrawLine(point + Vector3.left * 0.6f, point + Vector3.right * 0.6f);
    }
}
