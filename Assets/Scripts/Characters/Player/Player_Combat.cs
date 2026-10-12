using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Animator), typeof(PlayerMovements))]
public class Player_Combat : MonoBehaviour
{
    [SerializeField] private Vector2 damageNumberOffset = new Vector2(0f, 0.35f);
    private Animator anim;
    private PlayerMovements movement;
    private StatsManager stats;
    private bool attackInProgress;
    private bool hitPending;
    private float nextAttackTime;
    private float attackEndTime;
    private readonly HashSet<Enemy_Health> hitEnemies = new HashSet<Enemy_Health>();
    private readonly HashSet<Enemy_Health> killedEnemies = new HashSet<Enemy_Health>();
    private readonly HashSet<TreeBehaviour> hitTrees = new HashSet<TreeBehaviour>();

    private void Awake()
    {
        anim = GetComponent<Animator>();
        movement = GetComponent<PlayerMovements>();
        stats = StatsManager.GetOrCreate();
    }

    private void Update()
    {
        if (ArcherHero.Selected) return;
        if (SettingsUI.BlocksGameplay || TownQuestBoardUI.BlocksInput) return;
        if (attackInProgress)
        {
            if (Time.time >= attackEndTime)
                FinishAttack();
            return;
        }

        if (movement.InteractionLocked || Time.time < nextAttackTime)
            return;

        if (Input.GetKeyDown(stats.playerAttackKey)
            || (stats.allowLeftMouseAttack && Input.GetMouseButtonDown(0)
                && (UnityEngine.EventSystems.EventSystem.current == null
                    || !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())))
            BeginAttack();
    }

    private void BeginAttack()
    {
        attackInProgress = true;
        hitPending = true;
        attackEndTime = Time.time + stats.playerAttackFallbackDuration;
        hitEnemies.Clear();
        killedEnemies.Clear();
        movement.SetMovementLocked(true);
        hitTrees.Clear();
        anim.SetTrigger("attack");
        GameAudio.Play(GameAudio.Cue.Swing, transform.position);
    }

    // Add this animation event to the frame where the sword should hit.
    public void PerformAttack()
    {
        if (!attackInProgress || !hitPending)
            return;

        hitPending = false;
        Vector2 facing = movement.factionDirection >= 0 ? Vector2.right : Vector2.left;
        Vector2 hitPosition = (Vector2)transform.position + facing * stats.playerAttackHitOffset;
        Collider2D[] hits = Physics2D.OverlapCircleAll(hitPosition, stats.playerAttackHitRadius);

        foreach (Collider2D hit in hits)
        {
            // Ignore detection/tracking triggers. Only a real enemy hurtbox
            // should be affected by the player's sword.
            if (hit.isTrigger)
                continue;

            TreeBehaviour tree = hit.GetComponentInParent<TreeBehaviour>();
            if (tree != null && hitTrees.Add(tree))
                tree.TakeDamage(stats.playerAttackDamage);

            Enemy_Health enemy = hit.GetComponentInParent<Enemy_Health>();
            if (enemy != null && hitEnemies.Add(enemy))
            {
                int damage = stats.playerAttackDamage;
                bool critical = Random.value < stats.EffectiveCriticalChance;
                if (critical)
                    damage = Mathf.Max(1, Mathf.RoundToInt(damage * stats.EffectiveCriticalDamageMultiplier));

                int healthBefore = enemy.CurrentHealth;
                if (!enemy.ChangeHealth(-damage, true, true)) continue;
                GameAudio.Play(GameAudio.Cue.Hit, enemy.transform.position);
                Player_Health playerHealth = GetComponent<Player_Health>();
                DamageNumber.Show(hitPosition + damageNumberOffset, healthBefore - enemy.CurrentHealth,
                    false, critical, playerHealth != null && playerHealth.healthText != null
                        ? playerHealth.healthText.font : null);

                if (!enemy.IsDead)
                {
                    Enemy_Movement enemyMovement = enemy.GetComponent<Enemy_Movement>();
                    if (enemyMovement != null)
                    {
                        Vector2 knockbackDirection = (Vector2)(enemy.transform.position - transform.position);
                        if (knockbackDirection.sqrMagnitude <= 0.0001f)
                            knockbackDirection = facing;

                        enemyMovement.ApplyHitReaction(
                            knockbackDirection,
                            stats.playerAttackKnockbackForce,
                            stats.playerAttackKnockbackDuration,
                            stats.playerAttackStunDuration);
                    }
                }

                if (enemy.IsDead)
                    killedEnemies.Add(enemy);
            }
        }
    }

    // Add this animation event to the last frame of the attack.
    public void FinishAttack()
    {
        if (!attackInProgress)
            return;

        attackInProgress = false;
        hitPending = false;
        foreach (Enemy_Health enemy in killedEnemies)
        {
            if (enemy != null)
                enemy.CompleteDeath();
        }
        killedEnemies.Clear();
        hitEnemies.Clear();
        nextAttackTime = Time.time + stats.playerAttackCooldown;
        movement.SetMovementLocked(false);
    }

    private void OnDisable()
    {
        if (movement != null)
            movement.SetMovementLocked(false);
        attackInProgress = false;
        hitPending = false;
        foreach (Enemy_Health enemy in killedEnemies)
        {
            if (enemy != null)
                enemy.CompleteDeath();
        }
        killedEnemies.Clear();
        hitEnemies.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        PlayerMovements currentMovement = movement != null ? movement : GetComponent<PlayerMovements>();
        bool facingRight = currentMovement == null || currentMovement.factionDirection >= 0;
        Vector2 facing = facingRight ? Vector2.right : Vector2.left;
        float hitOffset = stats != null ? stats.playerAttackHitOffset : 0.5f;
        float hitRadius = stats != null ? stats.playerAttackHitRadius : 0.8f;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere((Vector2)transform.position + facing * hitOffset, hitRadius);
    }
}
