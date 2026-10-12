using System;
using System.Collections.Generic;

/// <summary>Quest state independent of views and Unity lifecycle, shared by hall and journal.</summary>
public sealed class TownQuestProgress
{
    public const string Goblins = "town_goblin_01";
    public const string Mushrooms = "town_mushroom_01";
    public const string Wood = "town_wood_01";
    public const string Meat = "town_meat_01";
    public const string Escort = "town_escort_jame_01";
    private readonly Dictionary<string, SavedTownQuest> states = new Dictionary<string, SavedTownQuest>(StringComparer.Ordinal);

    public static int Target(string id) => id == Escort ? 1 : id == Goblins || id == Meat ? 3 : id == Mushrooms ? 5 : id == Wood ? 10 : 0;
    public bool CompleteEscort()
    {
        if (!states.TryGetValue(Escort, out SavedTownQuest state) || state.completed) return false;
        state.progress = 1; state.completed = true; return true;
    }
    public static string ItemKey(string id) => id == Mushrooms ? "mushroom" : id == Wood ? "wood" : id == Meat ? "meet" : null;
    public bool IsAccepted(string id) => id != null && states.ContainsKey(id);
    public bool IsCompleted(string id) => id != null && states.TryGetValue(id, out SavedTownQuest s) && s.completed;

    public bool Accept(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || IsAccepted(id)) return false;
        states.Add(id, new SavedTownQuest { id = id });
        return true;
    }

    public bool RecordGoblinKill()
    {
        if (!states.TryGetValue(Goblins, out SavedTownQuest s) || s.completed || s.progress >= Target(Goblins)) return false;
        s.progress++;
        return true;
    }

    public int Progress(string id, int inventoryAmount)
    {
        if (!states.TryGetValue(id, out SavedTownQuest s)) return 0;
        if (s.completed) return Target(id);
        return Math.Min(Target(id), Math.Max(0, id == Goblins || id == Escort ? s.progress : inventoryAmount));
    }

    public bool CanTurnIn(string id, int inventoryAmount) => IsAccepted(id) && !IsCompleted(id)
        && Target(id) > 0 && Progress(id, inventoryAmount) >= Target(id);

    public bool TryTurnIn(string id, int inventoryAmount, Func<string, int, bool> consumeItems)
    {
        if (!CanTurnIn(id, inventoryAmount)) return false;
        SavedTownQuest state = states[id];
        // Mark before notifying inventory listeners so a reentrant click cannot submit twice.
        state.completed = true;
        string item = ItemKey(id);
        if (item != null)
        {
            try
            {
                if (consumeItems == null || !consumeItems(item, Target(id))) { state.completed = false; return false; }
            }
            catch { state.completed = false; throw; }
        }
        state.progress = Target(id);
        return true;
    }

    public string[] AcceptedIds()
    {
        var ids = new List<string>(states.Keys); ids.Sort(StringComparer.Ordinal); return ids.ToArray();
    }
    public SavedTownQuest[] Capture()
    {
        var result = new List<SavedTownQuest>();
        foreach (string id in AcceptedIds())
        {
            SavedTownQuest s = states[id];
            result.Add(new SavedTownQuest { id = id, progress = s.progress, completed = s.completed });
        }
        return result.ToArray();
    }
    public void Restore(string[] legacyAccepted, SavedTownQuest[] saved)
    {
        states.Clear();
        if (legacyAccepted != null) foreach (string id in legacyAccepted) Accept(id);
        if (saved == null) return;
        foreach (SavedTownQuest s in saved)
        {
            if (s == null || string.IsNullOrWhiteSpace(s.id)) continue;
            states[s.id] = new SavedTownQuest { id = s.id, progress = Math.Max(0, s.progress), completed = s.completed };
        }
    }
}
