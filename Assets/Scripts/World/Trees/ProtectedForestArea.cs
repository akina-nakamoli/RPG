using UnityEngine;

/// <summary>Protects only trees inside this local rectangle, not the shared tree prefab.</summary>
public class ProtectedForestArea : MonoBehaviour
{
    public Vector2 center = new Vector2(15f, -11f);
    public Vector2 size = new Vector2(16f, 14f);

    public bool Contains(Vector3 worldRoot)
    {
        Vector2 point = (Vector2)transform.InverseTransformPoint(worldRoot) - center;
        return Mathf.Abs(point.x) <= Mathf.Abs(size.x) * .5f
            && Mathf.Abs(point.y) <= Mathf.Abs(size.y) * .5f;
    }

    private void OnDrawGizmosSelected()
    {
        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, .65f, .1f, .9f);
        Gizmos.DrawWireCube(center, new Vector3(size.x, size.y, 0f));
        Gizmos.matrix = previous;
    }
}
