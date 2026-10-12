using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ExpManager : MonoBehaviour
{
    [SerializeField] private Slider expSlider;
    public RectTransform ExperienceRect => expSlider != null ? expSlider.transform as RectTransform : null;
    [SerializeField] private TMP_Text levelText;
    private StatsManager stats;

    public int CurrentLevel => StatsManager.GetOrCreate().playerLevel;
    public int CurrentExp => StatsManager.GetOrCreate().playerExperience;
    public int RequiredExp => GetRequiredExp(CurrentLevel);

    private void OnEnable()
    {
        stats = StatsManager.GetOrCreate();
        stats.ExperienceChanged += RefreshUI;
        RefreshUI();
    }

    private void OnDisable()
    {
        if (stats != null)
            stats.ExperienceChanged -= RefreshUI;
    }

    public static int GetRequiredExp(int level)
    {
        StatsManager data = StatsManager.GetOrCreate();
        long required = (long)Mathf.Max(1, data.baseLevelExperience)
            + (long)Mathf.Max(0, level) * Mathf.Max(0, data.experienceIncreasePerLevel);
        return (int)Math.Min(int.MaxValue, required);
    }

    // Shared entry point for enemy rewards, quests and pickups; works without an active UI.
    public static void AwardExperience(int amount)
    {
        if (amount <= 0) return;
        StatsManager data = StatsManager.GetOrCreate();
        int cap = Mathf.Max(1, data.maxPlayerLevel);
        data.playerLevel = Mathf.Clamp(data.playerLevel, 0, cap);
        long remaining = (long)Mathf.Max(0, data.playerExperience) + amount;
        bool leveledUp = false;
        while (data.playerLevel < cap)
        {
            int required = GetRequiredExp(data.playerLevel);
            if (remaining < required) break;
            remaining -= required;
            data.playerLevel++;
            data.skillPoints++;
            leveledUp = true;
        }
        data.playerExperience = data.playerLevel >= cap ? 0 : (int)remaining;
        data.NotifyExperienceChanged();
        if (leveledUp)
            data.NotifySkillTreeChanged();
    }

    public void AddExperience(int amount) => AwardExperience(amount);

    private void RefreshUI()
    {
        bool maxLevel = CurrentLevel >= Mathf.Max(1, stats.maxPlayerLevel);
        if (expSlider != null)
        {
            expSlider.interactable = false;
            expSlider.minValue = 0;
            expSlider.maxValue = RequiredExp;
            expSlider.wholeNumbers = true;
            expSlider.SetValueWithoutNotify(maxLevel ? RequiredExp : CurrentExp);
        }
        if (levelText != null)
            levelText.text = maxLevel ? $"LEVEL:{CurrentLevel} MAX" : $"LEVEL:{CurrentLevel}";
    }
}
