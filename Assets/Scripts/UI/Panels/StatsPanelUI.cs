using System.Globalization;
using TMPro;
using UnityEngine;

public sealed class StatsPanelUI : MonoBehaviour
{
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private TMP_Text defenseText;
    [SerializeField] private TMP_Text dodgeText;
    [SerializeField] private TMP_Text criticalDamageText;
    [SerializeField] private TMP_Text criticalChanceText;
    [SerializeField] private Player_Health playerHealth;

    private void OnEnable()
    {
        ResolvePlayer();
        Refresh();
    }

    // Read after gameplay/skill updates, and also refresh immediately on opening.
    private void LateUpdate() => Refresh();

    private void ResolvePlayer()
    {
        // Include a defeated/inactive player so the panel can still display 0 HP.
        if (playerHealth == null) playerHealth = FindObjectOfType<Player_Health>(true);
    }

    private void Refresh()
    {
        StatsManager stats = StatsManager.GetOrCreate();
        ResolvePlayer();
        Set(healthText, playerHealth != null
            ? "HP: " + playerHealth.CurrentHealth + " / " + playerHealth.MaxHealth
            : "HP: -- / " + stats.EffectiveMaxHealth);
        Set(defenseText, "DEF: " + Number(stats.EffectiveDefense));
        Set(dodgeText, "DR: " + Number(stats.EffectiveDodgeChance * 100f) + "%");
        // Show total critical damage: a 1.1 multiplier means 110% damage.
        Set(criticalDamageText, "CD: " + Number(stats.EffectiveCriticalDamageMultiplier * 100f) + "%");
        Set(criticalChanceText, "CRI: " + Number(stats.EffectiveCriticalChance * 100f) + "%");
    }

    private static string Number(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static void Set(TMP_Text label, string value)
    {
        if (label != null && label.text != value) label.text = value;
    }
}
