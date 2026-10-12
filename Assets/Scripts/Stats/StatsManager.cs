using UnityEngine;

public class StatsManager : MonoBehaviour
{
    public static StatsManager Instance { get; private set; }
    [Header("Experience")]
    [Min(0)] public int playerLevel = 0;
    [Min(0)] public int playerExperience = 0;
    [Min(1)] public int baseLevelExperience = 10;
    [Min(0)] public int experienceIncreasePerLevel = 5;
    [Min(1)] public int maxPlayerLevel = 100;
    [Min(0)] public int enemyExperienceReward = 5;
    public event System.Action ExperienceChanged;
    public event System.Action SkillTreeChanged;

    public void NotifyExperienceChanged() => ExperienceChanged?.Invoke();
    public void NotifySkillTreeChanged() => SkillTreeChanged?.Invoke();

    [Header("Player")]
    [Min(1)] public int playerMaxHealth = 100;
    [Min(1)] public int playerStartingHealth = 100;
    [Min(0f)] public float playerMoveSpeed = 5f;
    [Min(0f)] public float playerDamageInvulnerability = 0.35f;

    [Header("Player Combat Stats")]
    [Min(0f)] public float playerDefense = 0f;
    [Range(0f, 1f)] public float playerDodgeChance = 0.1f;
    [Range(0f, 1f)] public float playerCriticalChance = 0.1f;
    [Min(1f)] public float playerCriticalDamageMultiplier = 1.1f;
    [SerializeField, HideInInspector] private int combatDefaultsVersion;

    private void UpgradeCombatDefaults()
    {
        if (combatDefaultsVersion >= 1) return;
        // Older scene/editor instances keep serialized values even after C#
        // initializers change. Upgrade the exact previous default configuration.
        if (Mathf.Approximately(playerDodgeChance, 0f)
            && Mathf.Approximately(playerCriticalChance, 0f)
            && Mathf.Approximately(playerCriticalDamageMultiplier, 1.5f))
        {
            playerDodgeChance = 0.1f;
            playerCriticalChance = 0.1f;
            playerCriticalDamageMultiplier = 1.1f;
        }
        combatDefaultsVersion = 1;
    }

    private void OnValidate() => UpgradeCombatDefaults();

    [Header("Player Attack")]
    public KeyCode playerAttackKey = KeyCode.J;
    public bool allowLeftMouseAttack = true;
    [Min(0)] public int playerAttackDamage = 2;
    [Min(0.1f)] public float playerAttackHitRadius = 0.8f;
    [Min(0f)] public float playerAttackHitOffset = 0.5f;
    [Min(0f)] public float playerAttackCooldown = 0.2f;
    [Min(0.1f)] public float playerAttackFallbackDuration = 0.8f;
    [Min(0f)] public float playerAttackKnockbackForce = 5f;
    [Min(0f)] public float playerAttackKnockbackDuration = 0.15f;
    [Min(0f)] public float playerAttackStunDuration = 0.4f;

    [Header("Enemy")]
    [Min(1)] public int enemyMaxHealth = 10;
    [Min(0f)] public float enemyMoveSpeed = 4f;

    [Header("Enemy Attack")]
    [Min(0)] public int enemyAttackDamage = 2;
    [Min(0.1f)] public float enemyAttackHitRadius = 0.7f;
    [Min(0f)] public float enemyAttackHitOffset = 0.4f;
    [Min(0f)] public float enemyAttackRange = 1.5f;
    [Min(0f)] public float enemyAttackCooldown = 0.5f;
    [Min(0.1f)] public float enemyAttackFallbackDuration = 0.8f;
    [Min(0f)] public float enemyAttackKnockbackForce = 7f;
    [Min(0f)] public float enemyAttackKnockbackDuration = 0.2f;

    [Header("Skill Points")]
    [Min(0)] public int skillPoints = 0;

    [Header("Skill Tree Configuration")]
    [Min(0)] public int healthPerSkillLevel = 10;
    [Min(0f)] public float defensePerSkillLevel = 1f;
    [Range(0f, 1f)] public float dodgeChancePerSkillLevel = 0.02f;
    [Range(0f, 1f)] public float criticalChancePerSkillLevel = 0.02f;
    [Min(0f)] public float criticalDamagePerSkillLevel = 0.1f;
    [Min(0)] public int healthPassiveBonus = 25;
    [Min(0f)] public float defensePassiveBonus = 5f;
    [Range(0f, 1f)] public float dodgePassiveBonus = 0.1f;
    [Range(0f, 1f)] public float criticalChancePassiveBonus = 0.1f;
    [Min(0f)] public float criticalDamagePassiveBonus = 0.5f;

    // Left to right: HP, DEF, DR, CD (critical damage), CRI (critical chance).
    // Five columns, three tiers per column. Each node has five ranks.
    [SerializeField] private int[] skillTreeLevels = new int[15];

    public const int SkillColumnCount = 5;
    public const int SkillRowCount = 3;
    public const int SkillRanksPerNode = 5;

    public int EffectiveMaxHealth => Mathf.Max(1, playerMaxHealth + GetTotalSkillLevels(0) * healthPerSkillLevel
        + GetCompletedNodeCount(0) * healthPassiveBonus);
    public float EffectiveDefense => Mathf.Max(0f, playerDefense + GetTotalSkillLevels(1) * defensePerSkillLevel
        + GetCompletedNodeCount(1) * defensePassiveBonus);
    public float EffectiveDodgeChance => Mathf.Clamp01(playerDodgeChance + GetTotalSkillLevels(2) * dodgeChancePerSkillLevel
        + GetCompletedNodeCount(2) * dodgePassiveBonus);
    public float EffectiveCriticalChance => Mathf.Clamp01(playerCriticalChance + GetTotalSkillLevels(4) * criticalChancePerSkillLevel
        + GetCompletedNodeCount(4) * criticalChancePassiveBonus);
    public float EffectiveCriticalDamageMultiplier => Mathf.Max(1f, playerCriticalDamageMultiplier
        + GetTotalSkillLevels(3) * criticalDamagePerSkillLevel
        + GetCompletedNodeCount(3) * criticalDamagePassiveBonus);

    public int GetSkillLevel(int column, int row)
    {
        if (!IsValidSkillNode(column, row)) return 0;
        EnsureSkillTreeArray();
        return Mathf.Clamp(skillTreeLevels[column * SkillRowCount + row], 0, SkillRanksPerNode);
    }

    public bool IsValidSkillNode(int column, int row) =>
        column >= 0 && column < SkillColumnCount && row >= 0 && row < SkillRowCount;

    public bool IsSkillNodeUnlocked(int column, int row)
    {
        if (!IsValidSkillNode(column, row)) return false;
        return row == 0 || GetSkillLevel(column, row - 1) >= SkillRanksPerNode;
    }

    public bool TryUpgradeSkill(int column, int row)
    {
        if (!IsSkillNodeUnlocked(column, row) || skillPoints <= 0) return false;
        int current = GetSkillLevel(column, row);
        if (current >= SkillRanksPerNode) return false;

        EnsureSkillTreeArray();
        skillTreeLevels[column * SkillRowCount + row] = current + 1;
        skillPoints--;
        NotifySkillTreeChanged();
        return true;
    }

    private int GetTotalSkillLevels(int column)
    {
        int total = 0;
        for (int row = 0; row < SkillRowCount; row++)
            total += GetSkillLevel(column, row);
        return total;
    }

    private int GetCompletedNodeCount(int column)
    {
        int completed = 0;
        for (int row = 0; row < SkillRowCount; row++)
        {
            if (GetSkillLevel(column, row) >= SkillRanksPerNode)
                completed++;
        }
        return completed;
    }

    private void EnsureSkillTreeArray()
    {
        if (skillTreeLevels == null || skillTreeLevels.Length != SkillColumnCount * SkillRowCount)
            skillTreeLevels = new int[SkillColumnCount * SkillRowCount];
    }

    public int[] CaptureSkillLevels()
    {
        EnsureSkillTreeArray();
        return (int[])skillTreeLevels.Clone();
    }

    public void RestoreProgress(GameSaveData save)
    {
        playerLevel = Mathf.Clamp(save.level, 0, maxPlayerLevel);
        playerExperience = save.experience;
        skillPoints = save.skillPoints;
        skillTreeLevels = (int[])save.skills.Clone();
        NotifyExperienceChanged();
        NotifySkillTreeChanged();
    }

    public static void ReleaseForSceneLoad()
    {
        if (Instance == null) return;
        Destroy(Instance.gameObject);
        Instance = null;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        UpgradeCombatDefaults();
        DontDestroyOnLoad(gameObject);

        // Ensure the skill tree can bind its buttons even if the UI canvas
        // starts disabled or an older prefab instance has not refreshed yet.
        SkillTreeManager.GetOrCreate();
    }

    public static StatsManager GetOrCreate()
    {
        if (Instance != null)
        {
            Instance.UpgradeCombatDefaults();
            return Instance;
        }

        Instance = FindObjectOfType<StatsManager>();
        if (Instance != null)
        {
            Instance.UpgradeCombatDefaults();
            return Instance;
        }

        GameObject managerObject = new GameObject("StatsManager");
        Instance = managerObject.AddComponent<StatsManager>();
        return Instance;
    }
}
