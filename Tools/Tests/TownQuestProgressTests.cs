using System;
public static class TownQuestProgressTests
{
    static int count;
    static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        count++; Console.WriteLine("PASS " + label);
    }
    public static void Main()
    {
        var q = new TownQuestProgress();
        Check(!q.RecordGoblinKill(), "kills before acceptance ignored");
        Check(q.Accept(TownQuestProgress.Goblins), "accept goblins");
        Check(!q.Accept(TownQuestProgress.Goblins), "duplicate acceptance rejected");
        q.RecordGoblinKill(); q.RecordGoblinKill();
        Check(!q.CanTurnIn(TownQuestProgress.Goblins, 99), "two kills not ready");
        var loaded = new TownQuestProgress(); loaded.Restore(q.AcceptedIds(), q.Capture());
        Check(loaded.Progress(TownQuestProgress.Goblins, 0) == 2, "partial kill progress restored");
        q.RecordGoblinKill();
        Check(q.CanTurnIn(TownQuestProgress.Goblins, 0) && !q.IsCompleted(TownQuestProgress.Goblins), "ready is not completed");
        Check(!q.RecordGoblinKill(), "kill count capped");
        Check(q.TryTurnIn(TownQuestProgress.Goblins, 0, null), "goblin turn in");
        Check(!q.TryTurnIn(TownQuestProgress.Goblins, 0, null), "repeat turn in rejected");
        q.Accept(TownQuestProgress.Wood);
        Check(!q.CanTurnIn(TownQuestProgress.Wood, 9) && q.CanTurnIn(TownQuestProgress.Wood, 10), "wood threshold");
        Check(!q.TryTurnIn(TownQuestProgress.Wood, 10, (key,n) => false) && !q.IsCompleted(TownQuestProgress.Wood), "failed consumption stays active");
        int consumed = 0;
        Check(q.TryTurnIn(TownQuestProgress.Wood, 10, (key,n) => {
            Check(key == "wood" && n == 10, "exact material cost");
            Check(!q.TryTurnIn(TownQuestProgress.Wood, 10, (k,a) => true), "reentrant turn in rejected");
            consumed += n; return true;
        }), "wood turn in");
        Check(consumed == 10 && q.Progress(TownQuestProgress.Wood, 0) == 10, "completed progress retained after consumption");
        q.Accept(TownQuestProgress.Mushrooms);
        Check(!q.CanTurnIn(TownQuestProgress.Mushrooms, 4) && q.CanTurnIn(TownQuestProgress.Mushrooms, 5), "mushroom threshold");
        Check(!q.CanTurnIn(TownQuestProgress.Mushrooms, 4), "inventory decrease updates readiness");
        loaded.Restore(q.AcceptedIds(), q.Capture());
        Check(loaded.IsCompleted(TownQuestProgress.Wood) && loaded.IsCompleted(TownQuestProgress.Goblins) && loaded.AcceptedIds().Length == 3, "completed records survive restore");
        loaded.Restore(new[] { TownQuestProgress.Goblins }, null);
        Check(loaded.IsAccepted(TownQuestProgress.Goblins) && loaded.Progress(TownQuestProgress.Goblins,0) == 0 && !loaded.IsCompleted(TownQuestProgress.Goblins), "legacy acceptance restored");
        Check(!new TownQuestProgress().IsAccepted(TownQuestProgress.Wood), "new session isolated");
        Console.WriteLine(count + " checks passed.");
    }
}
