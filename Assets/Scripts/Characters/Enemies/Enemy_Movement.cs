using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Animator), typeof(Enemy_Combat))]
public class Enemy_Movement : MonoBehaviour
{
    private Rigidbody2D rb;
    private Animator anim;
    private SpriteRenderer sprite;
    private CircleCollider2D detectionRange;
    private float detectionOffsetX;
    private Enemy_Combat combat;
    private StatsManager stats;
    private Transform player;
    private EnemyState enemyState;
    private float nextAttackTime;
    private float attackEndTime;
    private Vector2 knockbackVelocity;
    private float knockbackEndTime;
    private float stunEndTime;
    private readonly HashSet<Collider2D> detected = new HashSet<Collider2D>();

    private void Awake()
    {
        WorldDepthSort.Ensure(gameObject);
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        sprite = GetComponent<SpriteRenderer>();
        detectionRange = GetComponent<CircleCollider2D>();
        if (detectionRange != null)
            detectionOffsetX = Mathf.Abs(detectionRange.offset.x);
        combat = GetComponent<Enemy_Combat>();
        stats = StatsManager.GetOrCreate();
    }

    private void OnEnable()
    {
        detected.Clear();
        player = null;
        nextAttackTime = 0f;
        knockbackVelocity = Vector2.zero;
        knockbackEndTime = 0f;
        stunEndTime = 0f;
        ChangeState(EnemyState.Idle);
    }

    private void FixedUpdate()
    {
        if (Time.time < knockbackEndTime)
        {
            rb.velocity = knockbackVelocity;
            return;
        }

        if (Time.time < stunEndTime)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        knockbackVelocity = Vector2.zero;

        if (enemyState == EnemyState.Attacking)
        {
            rb.velocity = Vector2.zero;
            if (Time.time >= attackEndTime)
                FinishAttack();
            return;
        }
        detected.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
        ChooseTarget();
        if (player == null || !player.gameObject.activeInHierarchy)
        {
            ChangeState(EnemyState.Idle);
            return;
        }
        Vector2 offset = player.position - transform.position;
        int attackDirection = Mathf.Abs(offset.y) > Mathf.Abs(offset.x) ? (offset.y > 0 ? 1 : 2) : 0;
        FaceTarget(offset);

        bool inAttackRange = offset.sqrMagnitude
            <= stats.enemyAttackRange * stats.enemyAttackRange;
        bool inHitRange = combat.IsTargetInHitRange(player, attackDirection, offset.x >= 0);
        if (inAttackRange && inHitRange)
        {
            ChangeState(EnemyState.Idle);
            if (Time.time >= nextAttackTime)
            {
                // 0: horizontal, 1: up, 2: down. Lock direction for this swing.
                anim.SetInteger("attackDirection", attackDirection);
                combat.BeginAttack(player, stats.enemyAttackRange, attackDirection, offset.x >= 0);
                attackEndTime = Time.time + stats.enemyAttackFallbackDuration;
                ChangeState(EnemyState.Attacking);
            }
            return;
        }
        ChangeState(EnemyState.Chasing);
        rb.velocity = offset.normalized * stats.enemyMoveSpeed;
    }

    private void FaceTarget(Vector2 offset)
    {
        // Move the detection collider with the enemy's facing direction.
        if (Mathf.Abs(offset.x) <= 0.01f)
            return;

        bool facingRight = offset.x > 0f;
        if (sprite != null)
            sprite.flipX = !facingRight;

        if (detectionRange != null)
        {
            Vector2 colliderOffset = detectionRange.offset;
            colliderOffset.x = facingRight ? detectionOffsetX : -detectionOffsetX;
            detectionRange.offset = colliderOffset;
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        var health = collision.GetComponentInParent<Player_Health>();
        if (health == null || !health.CompareTag("Player")) return;
        detected.Add(collision);
    }
    private void ChooseTarget()
    {
        player = null; float nearest = float.PositiveInfinity;
        foreach (var collider in detected)
        {
            var health = collider.GetComponentInParent<Player_Health>();
            if (health == null || health.CurrentHealth <= 0) continue;
            float distance = ((Vector2)health.transform.position - rb.position).sqrMagnitude;
            if (distance < nearest) { nearest = distance; player = health.transform; }
        }
        var jame = JameHealth.Active;
        if (jame != null && jame.CanBeAttacked && detectionRange != null && detectionRange.enabled && detectionRange.OverlapPoint(jame.transform.position))
        {
            float distance = ((Vector2)jame.transform.position - rb.position).sqrMagnitude;
            if (distance < nearest) player = jame.transform;
        }
    }

    private void OnTriggerExit2D(Collider2D collision) => detected.Remove(collision);

    // Called when the player successfully hits this enemy.
    // The enemy is pushed first, then remains unable to chase or attack until the stun ends.
    public void ApplyHitReaction(Vector2 direction, float force, float knockbackDuration, float stunDuration)
    {
        if (!enabled || !gameObject.activeInHierarchy)
            return;

        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector2.left;

        combat.CancelAttack();
        ChangeState(EnemyState.Idle);

        float now = Time.time;
        knockbackVelocity = direction.normalized * Mathf.Max(0f, force);
        knockbackEndTime = Mathf.Max(knockbackEndTime, now + Mathf.Max(0f, knockbackDuration));
        stunEndTime = Mathf.Max(stunEndTime, now + Mathf.Max(0f, stunDuration));
        nextAttackTime = Mathf.Max(nextAttackTime, stunEndTime);

        if (knockbackVelocity == Vector2.zero)
            rb.velocity = Vector2.zero;
    }

    // Animation event at the end of every attack.
    public void FinishAttack()
    {
        if (enemyState != EnemyState.Attacking) return;
        combat.ApplyPendingKnockback();
        combat.CancelAttack();
        nextAttackTime = Time.time + stats.enemyAttackCooldown;
        ChangeState(EnemyState.Idle);
    }

    private void ChangeState(EnemyState state)
    {
        enemyState = state;
        anim.SetBool("isMoving", state == EnemyState.Chasing);
        anim.SetBool("isAttacking", state == EnemyState.Attacking);
        if (state != EnemyState.Chasing) rb.velocity = Vector2.zero;
    }

    private void OnDisable()
    {
        combat.CancelAttack();
        rb.velocity = Vector2.zero;
        knockbackVelocity = Vector2.zero;
        knockbackEndTime = 0f;
        stunEndTime = 0f;
        detected.Clear();
        player = null;
    }
}

public enum EnemyState { Idle, Chasing, Attacking }
