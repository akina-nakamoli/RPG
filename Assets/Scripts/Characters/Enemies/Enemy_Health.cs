using UnityEngine;

public class Enemy_Health : MonoBehaviour
{
    [Tooltip("Only Goblin enemies count toward the town's Goblin Hunt quest.")]
    [SerializeField] private bool isGoblin;
    [SerializeField] private Loot deathDropPrefab;
    [Min(1)] [SerializeField] private int deathDropQuantity = 1;
    [Header("Bonus Gold Drop (player kills only)")]
    [SerializeField] private Loot goldDropPrefab;
    [Range(0f, 1f)] [SerializeField] private float goldDropChance = 0.15f;
    [Min(1)] [SerializeField] private int goldDropQuantity = 1;
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    private int currentHealth;
    private int maxHealth;
    private bool deathPending;
    private bool experienceAwarded;
    private bool deathDropSpawned;
    private bool killedByPlayer;
    private bool goldDropRolled;
    private Vector3 deathPosition;
    private StatsManager stats;

    public bool IsDead => currentHealth <= 0;

    private void Awake()
    {
        stats = StatsManager.GetOrCreate();
        InitializeHealth();
        if (isGoblin) gameObject.AddComponent<ActorHealthBar>().Configure(() => CurrentHealth, () => MaxHealth);
    }

    private void OnEnable()
    {
        if (stats == null)
        {
            stats = StatsManager.GetOrCreate();
            InitializeHealth();
        }
    }

    private void InitializeHealth()
    {
        maxHealth = Mathf.Max(1, stats.enemyMaxHealth);
        currentHealth = maxHealth;
        deathPending = false;
        experienceAwarded = false;
        deathDropSpawned = false;
        killedByPlayer = false;
        goldDropRolled = false;
    }

    public bool ChangeHealth(int amount)
    {
        return ChangeHealth(amount, false);
    }

    public bool ChangeHealth(int amount, bool deferDeath, bool damagedByPlayer = false)
    {
        if (IsDead)
            return false;

        currentHealth = Mathf.Clamp(currentHealth + amount, 0, maxHealth);

        if (currentHealth <= 0)
        {
            // Capture the lethal hit position before deferred death/physics can
            // move the enemy. The drop must stay where the enemy was killed.
            deathPosition = transform.position;
            GetComponent<ForestGoblinIdentity>()?.RecordDeath();
            GameAudio.Play(GameAudio.Cue.Death, deathPosition);
            killedByPlayer = damagedByPlayer;
            if (!experienceAwarded)
            {
                experienceAwarded = true;
                if (isGoblin && damagedByPlayer)
                {
                    TownQuestBoardUI board = TownQuestBoardUI.FindOrCreate();
                    if (board != null) board.RecordGoblinKill();
                }
                ExpManager.AwardExperience(stats.enemyExperienceReward);
            }
            if (deferDeath)
            {
                deathPending = true;
                Enemy_Movement movement = GetComponent<Enemy_Movement>();
                if (movement != null)
                    movement.enabled = false;
            }
            else
            {
                CompleteDeath();
            }
        }

        return true;
    }

    public void CompleteDeath()
    {
        if (!deathPending && !IsDead)
            return;

        deathPending = false;
        if (isGoblin && !deathDropSpawned && deathDropPrefab != null)
        {
            deathDropSpawned = Loot.SpawnDeathDrop(deathDropPrefab, deathPosition, deathDropQuantity) != null;
        }
        if (killedByPlayer && !goldDropRolled)
        {
            // Roll once per death, even if CompleteDeath is called again.
            goldDropRolled = true;
            if (goldDropPrefab != null && Random.value < Mathf.Clamp01(goldDropChance))
            {
                Vector3 coinPosition = deathPosition;
                if (deathDropSpawned) coinPosition.x += 0.45f;
                Loot.SpawnDeathDrop(goldDropPrefab, coinPosition, goldDropQuantity);
            }
        }
        gameObject.SetActive(false);
    }
}
