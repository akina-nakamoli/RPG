using UnityEngine;

/// <summary>Shared ground-contact sorting, independent of the camera and player.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]
public class WorldDepthSort : MonoBehaviour
{
    private SpriteRenderer[] visuals;
    private int[] offsets;
    private Vector3 localGround;
    private bool elevated;

    public static int OrderAt(float groundY)
    {
        // Reserve 15000/15001 for raised terrain, and 16000+ for its occupants.
        return Mathf.Clamp(10000 - Mathf.RoundToInt(groundY * 100f), 100, 14000);
    }

    public void SetElevated(bool value)
    {
        elevated = value;
        ApplyOrder();
    }

    public void RestoreElevation(int savedOrder)
    {
        SetElevated(savedOrder >= 16000 || savedOrder == 15);
    }

    public static void Ensure(GameObject target)
    {
        if (target.GetComponent<WorldDepthSort>() == null)
            target.AddComponent<WorldDepthSort>();
    }

    private void Awake()
    {
        visuals = GetComponentsInChildren<SpriteRenderer>(true);
        offsets = new int[visuals.Length];
        int initial = visuals.Length > 0 ? visuals[0].sortingOrder : 0;
        for (int i = 0; i < visuals.Length; i++)
            offsets[i] = Mathf.Clamp(visuals[i].sortingOrder - initial, -20, 20);

        // Loot uses its stationary root, not the bouncing pickup animation or trigger.
        if (GetComponent<Loot>() != null) return;
        foreach (Collider2D body in GetComponents<Collider2D>())
        {
            if (body.isTrigger || !body.enabled) continue;
            localGround = transform.InverseTransformPoint(new Vector3(
                body.bounds.center.x, body.bounds.min.y, transform.position.z));
            break;
        }
    }

    private void LateUpdate()
    {
        ApplyOrder();
    }

    private void ApplyOrder()
    {
        if (visuals == null) return;
        int order = OrderAt(transform.TransformPoint(localGround).y);
        if (elevated) order += 15900;
        for (int i = 0; i < visuals.Length; i++)
            if (visuals[i] != null) visuals[i].sortingOrder = order + offsets[i];
    }
}
