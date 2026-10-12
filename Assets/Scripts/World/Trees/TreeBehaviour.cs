using UnityEngine;

[RequireComponent(typeof(SpriteRenderer), typeof(Animator), typeof(CapsuleCollider2D))]
[ExecuteAlways]
public class TreeBehaviour : MonoBehaviour
{
    [Header("Chopping")]
    [Tooltip("Disable for decorative or boundary trees. Protected forest areas also prevent chopping.")]
    public bool canBeChopped = true;
    [Min(1)] public int maxHealth = 6;
    [Min(1)] public int woodQuantity = 3;
    public Loot woodPrefab;
    public Sprite stumpSprite;
    [Header("Depth Sorting")]
    [Tooltip("Local position where the trunk meets the ground.")]
    public Vector2 rootOffset = new Vector2(0.004f, -0.42f);
    public int treeBaseOrder = 11;
    private SpriteRenderer visual;
    private CapsuleCollider2D trunk;
    private Animator animator;
    private int health;
    private bool chopped, reacting, dropped;
    private int activeState;
    private static readonly int Idle = Animator.StringToHash("Base Layer.Idle");
    private static readonly int Hit = Animator.StringToHash("Base Layer.Hit");
    private static readonly int Chopped = Animator.StringToHash("Base Layer.Chopped");
    private Vector3 Root => transform.TransformPoint(rootOffset);

    private void Awake()
    {
        visual = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        trunk = GetComponent<CapsuleCollider2D>();
        visual.drawMode = SpriteDrawMode.Simple;
        health = Mathf.Max(1, maxHealth);
    }

    public void TakeDamage(int damage)
    {
        if (!canBeChopped || chopped || damage <= 0) return;
        foreach (ProtectedForestArea area in GetComponentsInParent<ProtectedForestArea>())
            if (area.enabled && area.Contains(Root)) return;
        health = Mathf.Max(0, health - damage);
        chopped = health == 0;
        GameAudio.Play(chopped ? GameAudio.Cue.TreeFall : GameAudio.Cue.Chop, Root);
        activeState = chopped ? Chopped : Hit;
        reacting = true;
        if (animator.runtimeAnimatorController != null && animator.HasState(0, activeState))
            animator.Play(activeState, 0, 0f);
        else FinishReaction();
    }

    private void Update()
    {
        if (!reacting) return;
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.fullPathHash == activeState && state.normalizedTime >= 1f) FinishReaction();
    }

    private void FinishReaction()
    {
        reacting = false;
        if (!chopped) { animator.Play(Idle, 0, 0f); return; }
        animator.enabled = false;
        if (stumpSprite != null) visual.sprite = stumpSprite;
        // Keep the base collider active after chopping so the stump remains solid.
        trunk.enabled = true;
        if (dropped) return;
        dropped = true;
        if (woodPrefab == null) return;
        // Preserve the loot prefab's existing root/child depth convention.
        Vector3 position = Root + Vector3.down * 0.35f;
        position.z = woodPrefab.transform.position.z;
        Loot loot = Instantiate(woodPrefab, position, Quaternion.identity);
        loot.quantity = Mathf.Max(1, woodQuantity);
    }

    private void LateUpdate()
    {
        if (visual == null) visual = GetComponent<SpriteRenderer>();
        if (visual == null) return;
        visual.sortingOrder = Application.isPlaying ? WorldDepthSort.OrderAt(Root.y) : treeBaseOrder;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(Root, 0.08f);
        Gizmos.DrawLine(Root + Vector3.left * .5f, Root + Vector3.right * .5f);
    }
}
