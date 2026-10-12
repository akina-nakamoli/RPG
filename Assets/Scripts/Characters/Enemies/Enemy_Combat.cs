using UnityEngine;

public class Enemy_Combat : MonoBehaviour
{
    [SerializeField] private Vector2 damageNumberOffset = new Vector2(0f, 0.35f);
    private StatsManager stats;
    private Transform target;
    private float range;
    private Vector2 direction;
    private bool hitPending;
    private PlayerMovements pendingKnockbackTarget;
    private Vector2 pendingKnockbackDirection;
    private bool knockbackPending;

    private void Awake()
    {
        stats = StatsManager.GetOrCreate();
    }

    public void BeginAttack(Transform player, float attackRange, int attackDirection, bool facingRight)
    {
        target = player;
        range = attackRange;
        direction = GetAttackDirection(attackDirection, facingRight);
        hitPending = true;
        knockbackPending = false;
        pendingKnockbackTarget = null;
    }

    public bool IsTargetInHitRange(Transform player, int attackDirection, bool facingRight)
    {
        if (player == null || !player.gameObject.activeInHierarchy)
            return false;

        Vector2 hitPosition = (Vector2)transform.position
            + GetAttackDirection(attackDirection, facingRight) * stats.enemyAttackHitOffset;
        Vector2 targetPosition = player.position;
        return (targetPosition - hitPosition).sqrMagnitude
            <= stats.enemyAttackHitRadius * stats.enemyAttackHitRadius;
    }

    private Vector2 GetAttackDirection(int attackDirection, bool facingRight)
    {
        return attackDirection == 1 ? Vector2.up : attackDirection == 2 ? Vector2.down
            : facingRight ? Vector2.right : Vector2.left;
    }

    // Animation event: one hit per swing; players can dodge the range/direction.
    public void DealAttackDamage()
    {
        if (!hitPending) return;
        hitPending = false;
        if (target == null || !target.gameObject.activeInHierarchy) return;
        Vector2 offset = target.position - transform.position;
        if (offset.sqrMagnitude > range * range) return;

        Vector2 hitPosition = (Vector2)transform.position
            + direction * stats.enemyAttackHitOffset;
        JameHealth jame = target.GetComponent<JameHealth>();
        if (jame != null)
        {
            if (((Vector2)target.position - hitPosition).sqrMagnitude <= stats.enemyAttackHitRadius * stats.enemyAttackHitRadius)
                jame.TakeDamage(stats.enemyAttackDamage);
            return;
        }
        Collider2D[] hits = Physics2D.OverlapCircleAll(hitPosition, stats.enemyAttackHitRadius);
        foreach (Collider2D hit in hits)
        {
            if (hit.isTrigger)
                continue;

            Player_Health health = hit.GetComponentInParent<Player_Health>();
            if (health != null && health.transform == target)
            {
                int healthBefore = health.CurrentHealth;
                if (health.ChangeHealth(-stats.enemyAttackDamage))
                {
                    DamageNumber.Show(hitPosition + damageNumberOffset, healthBefore - health.CurrentHealth,
                        true, false, health.healthText != null ? health.healthText.font : null);
                    PlayerMovements movement = health.GetComponentInParent<PlayerMovements>();
                    if (movement != null)
                    {
                        Vector2 knockbackDirection = (target.position - transform.position).normalized;
                        if (knockbackDirection.sqrMagnitude <= 0.0001f)
                            knockbackDirection = direction;

                        // Store the knockback until the attack animation has finished.
                        pendingKnockbackTarget = movement;
                        pendingKnockbackDirection = knockbackDirection;
                        knockbackPending = true;
                    }
                }
                return;
            }
        }
    }

    // Called by Enemy_Movement when the attack animation reaches its end.
    public void ApplyPendingKnockback()
    {
        if (!knockbackPending)
            return;

        if (pendingKnockbackTarget != null && pendingKnockbackTarget.gameObject.activeInHierarchy)
        {
                pendingKnockbackTarget.ApplyKnockback(
                    pendingKnockbackDirection,
                    stats.enemyAttackKnockbackForce,
                    stats.enemyAttackKnockbackDuration);
        }

        ClearPendingKnockback();
    }

    public void CancelAttack()
    {
        hitPending = false;
        target = null;
        ClearPendingKnockback();
    }

    private void ClearPendingKnockback()
    {
        pendingKnockbackTarget = null;
        pendingKnockbackDirection = Vector2.zero;
        knockbackPending = false;
    }

    private void OnDisable() => CancelAttack();
}
