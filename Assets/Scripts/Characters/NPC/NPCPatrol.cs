using System.Collections.Generic;
using UnityEngine;

/// <summary>Local reachable-area search with swept-body clearance and dynamic replanning.</summary>
[RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D), typeof(Animator))]
public class NPCPatrol : MonoBehaviour
{
    [Header("Rectangular Patrol Area")]
    [Tooltip("Full width (X) and height (Y) of the patrol rectangle.")]
    public Vector2 patrolAreaSize = new Vector2(10f, 10f);
    [Tooltip("Use the NPC's starting position as the area center. Turn off to place the area independently in world coordinates.")]
    public bool centerAreaAtNpcStart = true;
    [Tooltip("Fixed world-space center of the patrol rectangle, used when Center Area At Npc Start is off.")]
    public Vector2 patrolAreaCenter;
    [Min(0f)] public float moveSpeed = 1.5f;
    public Vector2 waitSeconds = new Vector2(1f, 3f);
    [Header("Obstacle Avoidance")]
    public LayerMask obstacleLayers = ~0;
    [Range(0.2f, 1f)] public float gridSize = 0.5f;
    [Min(0.01f)] public float clearance = 0.04f;
    [Min(0.2f)] public float replanDelay = 0.5f;
    [Range(64, 4096)] public int maxSearchNodes = 1600;
    [Header("Animation State Names")]
    public string idleState = "Idle";
    public string runState = "Run";

    private Rigidbody2D body;
    private CapsuleCollider2D shape;
    private Animator animator;
    private SpriteRenderer sprite;
    private Vector2 home, checkpoint;
    private float nextPlanTime, nextStuckCheck;
    private int animationHash;
    private readonly List<Vector2> path = new List<Vector2>();
    private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>();
    private readonly List<Collider2D> overlaps = new List<Collider2D>();
    private readonly Dictionary<Vector2Int, Vector2Int> parents = new Dictionary<Vector2Int, Vector2Int>();
    private readonly Queue<Vector2Int> frontier = new Queue<Vector2Int>();
    private readonly List<Vector2Int> candidates = new List<Vector2Int>();
    private ContactFilter2D filter;
    private float bodyRadius;
    private Vector2 centerOffset;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        shape = GetComponent<CapsuleCollider2D>();
        animator = GetComponent<Animator>();
        sprite = GetComponent<SpriteRenderer>();
        body.gravityScale = 0;
        body.constraints |= RigidbodyConstraints2D.FreezeRotation;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        home = centerAreaAtNpcStart ? body.position : patrolAreaCenter;
    }

    private void OnEnable()
    {
        path.Clear();
        nextPlanTime = Time.time + 0.2f;
        checkpoint = body.position;
        nextStuckCheck = Time.time + 1f;
    }

    private void FixedUpdate()
    {
        filter = new ContactFilter2D { useLayerMask = true, layerMask = obstacleLayers, useTriggers = false };
        // A circle enclosing the real capsule keeps corners clear even when the sprite flips.
        bodyRadius = Mathf.Max(shape.bounds.extents.x, shape.bounds.extents.y) + clearance;
        centerOffset = (Vector2)shape.bounds.center - body.position;
        body.velocity = Vector2.zero;
        if (moveSpeed <= 0) { Animate(Vector2.zero); return; }
        if (path.Count == 0)
        {
            Animate(Vector2.zero);
            if (Time.time >= nextPlanTime) PlanRoute();
            return;
        }
        // Skip unnecessary grid corners only when the entire swept body fits.
        for (int i = path.Count - 1; i > 0; i--)
        {
            if (ClearSegment(body.position, path[i])) { path.RemoveRange(0, i); break; }
        }
        Vector2 delta = path[0] - body.position;
        if (delta.magnitude < 0.06f)
        {
            path.RemoveAt(0);
            if (path.Count == 0) Stop(Random.Range(Mathf.Max(0, waitSeconds.x), Mathf.Max(waitSeconds.x, waitSeconds.y)));
            return;
        }
        Vector2 step = Vector2.ClampMagnitude(delta, moveSpeed * Time.fixedDeltaTime);
        if (!Inside(body.position + step) || !ClearSegment(body.position, body.position + step))
        {
            Stop(replanDelay);
            return;
        }
        body.MovePosition(body.position + step);
        Animate(step);
        if (Time.time >= nextStuckCheck)
        {
            if (Vector2.Distance(checkpoint, body.position) < Mathf.Min(0.1f, moveSpeed * 0.25f)) Stop(replanDelay);
            checkpoint = body.position;
            nextStuckCheck = Time.time + 1f;
        }
    }

    private bool Inside(Vector2 point)
    {
        Vector2 offset = point - home;
        float halfWidth = Mathf.Max(0.5f, patrolAreaSize.x * 0.5f);
        float halfHeight = Mathf.Max(0.5f, patrolAreaSize.y * 0.5f);
        return Mathf.Abs(offset.x) <= halfWidth && Mathf.Abs(offset.y) <= halfHeight;
    }

    private bool Blocks(Collider2D collider)
    {
        return collider != null && collider.attachedRigidbody != body
            && !collider.transform.IsChildOf(transform)
            && !Physics2D.GetIgnoreCollision(shape, collider)
            && !Physics2D.GetIgnoreLayerCollision(gameObject.layer, collider.gameObject.layer);
    }

    private bool ClearPoint(Vector2 point)
    {
        Physics2D.OverlapCircle(point + centerOffset, bodyRadius, filter, overlaps);
        foreach (Collider2D collider in overlaps) if (Blocks(collider)) return false;
        return true;
    }

    private bool ClearSegment(Vector2 start, Vector2 end)
    {
        Vector2 delta = end - start;
        Physics2D.CircleCast(start + centerOffset, bodyRadius, delta.normalized, filter, hits, delta.magnitude);
        foreach (RaycastHit2D hit in hits) if (Blocks(hit.collider)) return false;
        return true;
    }

    private void PlanRoute()
    {
        path.Clear(); parents.Clear(); frontier.Clear(); candidates.Clear();
        Vector2 origin = body.position;
        Vector2Int root = Vector2Int.zero;
        parents[root] = root;
        frontier.Enqueue(root);
        // Breadth-first search chooses only destinations connected to our current position.
        while (frontier.Count > 0 && parents.Count < Mathf.Max(64, maxSearchNodes))
        {
            Vector2Int cell = frontier.Dequeue();
            Vector2 from = origin + (Vector2)cell * gridSize;
            for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0) continue;
                Vector2Int next = cell + new Vector2Int(x, y);
                if (parents.ContainsKey(next)) continue;
                Vector2 to = origin + (Vector2)next * gridSize;
                if (!Inside(to) || !ClearPoint(to) || !ClearSegment(from, to)) continue;
                parents[next] = cell;
                frontier.Enqueue(next);
                if ((to - origin).sqrMagnitude >= gridSize * gridSize * 4) candidates.Add(next);
            }
        }
        if (candidates.Count == 0) { Stop(1f); return; }
        Vector2Int target = candidates[Random.Range(0, candidates.Count)];
        while (target != root)
        {
            path.Add(origin + (Vector2)target * gridSize);
            target = parents[target];
        }
        path.Reverse();
        checkpoint = body.position;
        nextStuckCheck = Time.time + 1f;
    }

    private void Stop(float delay)
    {
        path.Clear(); body.velocity = Vector2.zero;
        nextPlanTime = Time.time + Mathf.Max(0.2f, delay);
        Animate(Vector2.zero);
    }

    private void Animate(Vector2 motion)
    {
        if (sprite != null && Mathf.Abs(motion.x) > 0.001f) sprite.flipX = motion.x < 0;
        int hash = Animator.StringToHash("Base Layer." + (motion.sqrMagnitude > 0.000001f ? runState : idleState));
        if (animator.runtimeAnimatorController == null || !animator.HasState(0, hash)) return;
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (animationHash != hash || (!state.loop && state.normalizedTime >= 1f)) animator.Play(hash, 0, 0);
        animationHash = hash;
    }

    private void OnDisable()
    {
        if (body != null) body.velocity = Vector2.zero;
        path.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector2 areaCenter = Application.isPlaying
            ? home
            : (centerAreaAtNpcStart ? (Vector2)transform.position : patrolAreaCenter);
        Vector3 center = new Vector3(areaCenter.x, areaCenter.y, transform.position.z);
        Gizmos.DrawWireCube(center, new Vector3(
            Mathf.Max(1f, patrolAreaSize.x), Mathf.Max(1f, patrolAreaSize.y), 0f));
        Gizmos.color = Color.yellow;
        Vector3 previous = transform.position;
        foreach (Vector2 point in path)
        {
            Vector3 next = new Vector3(point.x, point.y, transform.position.z);
            Gizmos.DrawLine(previous, next); previous = next;
        }
    }
}
