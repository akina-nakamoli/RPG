using UnityEngine;
using TMPro;

public class Player_Health : MonoBehaviour
{
    public TMP_Text healthText;
    public Animator healthTextAnim;
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    private bool defeated;

    public void RestoreHealth(int savedHealth)
    {
        maxHealth = stats.EffectiveMaxHealth;
        currentHealth = Mathf.Clamp(savedHealth, 1, maxHealth);
        nextDamageTime = 0f;
        defeated = false;
        UpdateHealthText();
    }

    private int currentHealth;
    private int maxHealth;
    private float damageInvulnerability;
    private StatsManager stats;
    private float nextDamageTime;

    private void Awake()
    {
        stats = StatsManager.GetOrCreate();
        maxHealth = stats.EffectiveMaxHealth;
        currentHealth = Mathf.Clamp(stats.playerStartingHealth, 0, maxHealth);
        damageInvulnerability = Mathf.Max(0f, stats.playerDamageInvulnerability);
    }

    private void OnEnable()
    {
        if (stats == null)
            stats = StatsManager.GetOrCreate();
        stats.SkillTreeChanged += RefreshStats;
    }

    private void OnDisable()
    {
        if (stats != null)
            stats.SkillTreeChanged -= RefreshStats;
    }

    private void RefreshStats()
    {
        int oldMaxHealth = maxHealth;
        maxHealth = stats.EffectiveMaxHealth;
        currentHealth = Mathf.Clamp(currentHealth + (maxHealth - oldMaxHealth), 0, maxHealth);
        damageInvulnerability = Mathf.Max(0f, stats.playerDamageInvulnerability);
        UpdateHealthText();
    }

    private void Start()
    {
        if (healthText != null)
            healthText.text = "HP:" + currentHealth + "/" + maxHealth;
    }
    public bool ChangeHealth(int amount)
    {
        if (defeated || (amount < 0 && (LocalGameSession.IsLoading || SceneTravel.IsTravelling || DeathPanelUI.IsOpen)))
            return false;
        if (amount < 0)
        {
            if (Time.time < nextDamageTime)
                return false;

            if (Random.value < stats.EffectiveDodgeChance)
                return false;

            int incomingDamage = -amount;
            amount = -Mathf.Max(1, Mathf.RoundToInt(incomingDamage - stats.EffectiveDefense));
        }

        if (amount < 0)
            nextDamageTime = Time.time + damageInvulnerability;

        currentHealth = Mathf.Clamp(currentHealth + amount, 0, maxHealth);
        if (amount < 0) GameAudio.Play(currentHealth <= 0 ? GameAudio.Cue.Death : GameAudio.Cue.Hurt, transform.position);
        UpdateHealthText();
        if (currentHealth <= 0)
        {
            Defeat();
        }

        return true;
    }

    private void Defeat()
    {
        if (defeated) return;
        defeated = true;
        DeathPanelUI.Show(this);
        gameObject.SetActive(false);
    }

    [ContextMenu("Test Death (Play Mode Only)")]
    private void TestDeath()
    {
        if (!Application.isPlaying || defeated || LocalGameSession.IsLoading || SceneTravel.IsTravelling) return;
        currentHealth = 0;
        UpdateHealthText();
        GameAudio.Play(GameAudio.Cue.Death, transform.position);
        Defeat();
    }

    private void UpdateHealthText()
    {
        if (healthTextAnim != null)
            healthTextAnim.Play("HealthUI_TextUpdate");
        if (healthText != null)
            healthText.text = "HP:" + currentHealth + "/" + maxHealth;
    }
        
}
