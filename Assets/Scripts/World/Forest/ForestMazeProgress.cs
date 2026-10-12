using System.Collections.Generic;
using UnityEngine;

public static class ForestMazeProgress
{
    private static readonly HashSet<string> defeated = new HashSet<string>();
    public static bool ChestOpened;
    public static bool IsDefeated(string id) => defeated.Contains(id);
    public static void MarkDefeated(string id) { if (!string.IsNullOrEmpty(id)) defeated.Add(id); }
    public static string[] Capture() { var result = new string[defeated.Count]; defeated.CopyTo(result); System.Array.Sort(result); return result; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset() { defeated.Clear(); ChestOpened = false; }
    public static void Restore(GameSaveData save)
    {
        Reset();
        if (save == null) return;
        foreach (string id in save.defeatedForestGoblins ?? new string[0]) MarkDefeated(id);
        ChestOpened = save.forestChestOpened;
    }
}
