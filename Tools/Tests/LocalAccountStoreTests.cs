// Standalone .NET storage tests. Serializer adapter is ONLY used outside Unity.
using System;
using System.IO;
using System.Web.Script.Serialization;
namespace UnityEngine
{
    // Standalone storage tests deliberately have no cloud configuration and never contact the live backend.
    public sealed class TextAsset { public string text; }
    public static class Resources
    {
        public static T Load<T>(string path) where T : class => null;
    }
    public static class JsonUtility
    {
        public static string ToJson(object value, bool prettyPrint = false) => new JavaScriptSerializer().Serialize(value);
        public static T FromJson<T>(string json) => new JavaScriptSerializer().Deserialize<T>(json);
    }
}
public static class LocalAccountStoreTests
{
    private static int count;
    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception("FAILED: " + description);
        count++; Console.WriteLine("PASS " + description);
    }
    private static void Reject(Action action, string description)
    {
        bool rejected = false;
        try { action(); } catch { rejected = true; }
        Check(rejected, description);
    }
    public static int Main(string[] args)
    {
        string root = Path.Combine(Path.GetFullPath(args[0]), "storage-tests-" + Guid.NewGuid().ToString("N"));
        var store = new LocalAccountStore(root);
        const string password = "NotARealPassword!42";
        LocalAccount alice = store.Register("Alice", password);
        Check(store.Login(" alice ", password).id == alice.id, "registration and case-insensitive login");
        Reject(() => store.Register("ALICE", password), "duplicate registration rejected");
        Reject(() => store.Login("Alice", "incorrect"), "wrong password rejected");
        Reject(() => store.Register("../../escape", password), "username traversal rejected");
        Reject(() => store.Register("Short", "tiny"), "short password rejected");
        LocalAccount bob = store.Register("Bob", password);
        Check(alice.salt != bob.salt && alice.passwordHash != bob.passwordHash, "unique salted password hashes");
        string accountsPath = Path.Combine(root, "accounts.json");
        Check(!File.ReadAllText(accountsPath).Contains(password), "password is not stored in plaintext");
        Check(!store.HasSave(alice.id, 1), "new slot is empty");
        Reject(() => store.HasSave("../escape", 1), "account path traversal rejected");
        Reject(() => store.HasSave(alice.id, 4), "invalid slot rejected");
        var data = new GameSaveData
        {
            accountId = alice.id, slot = 1, savedUtc = DateTime.UtcNow.ToString("O"),
            health = 43, level = 2, experience = 7, skillPoints = 3, x = 12.3f, y = -4.5f,
            items = new[] { new SavedItem { key = "gold", amount = 123 }, new SavedItem { key = "wood", amount = 9 } },
            completedGreetings = new[] { "NPC_1[1]" },
            acceptedTownQuests = new[] { "town_wood_01", "town_mushroom_01" },
            townQuestStates = new[] { new SavedTownQuest { id = "town_wood_01", progress = 10, completed = true }, new SavedTownQuest { id = "town_goblin_01", progress = 2 } },
            terrainColliders = new[] { new SavedCollider { key = "Terrain[3]:0", enabled = true } }
        };
        data.skills[0] = 5; data.skills[1] = 2;
        store.Save(data);
        GameSaveData loaded = store.Load(alice.id, 1);
        Check(loaded.health == 43 && loaded.level == 2 && loaded.items[1].amount == 9 && loaded.skills[1] == 2
            && loaded.completedGreetings[0] == "NPC_1[1]" && loaded.terrainColliders[0].enabled,
            "save round-trip: progress, inventory, skills, NPC and terrain state");
        Check(!store.HasSave(bob.id, 1), "accounts have isolated saves");
        Check(!store.HasSave(alice.id, 2), "slots are isolated");
        string savePath = Path.Combine(root, alice.id, "slot-1.json");
        string original = File.ReadAllText(savePath);
        Check(loaded.townQuestStates[0].completed && loaded.townQuestStates[0].progress == 10 && loaded.townQuestStates[1].progress == 2,
            "completed quests and partial kills survive disk round-trip");
        data.townQuestStates[0].progress = 9;
        Reject(() => store.Save(data), "inconsistent completed progress rejected");
        data.townQuestStates[0].progress = 10;
        Check(loaded.acceptedTownQuests.Length == 2 && loaded.acceptedTownQuests[0] == "town_wood_01",
            "accepted town quests survive a save/load round-trip");
        data.acceptedTownQuests = new[] { "town_wood_01", "town_wood_01" };
        Reject(() => store.Save(data), "duplicate accepted quest IDs rejected");
        Check(File.ReadAllText(savePath) == original, "invalid quest data does not overwrite the save");
        data.acceptedTownQuests = new[] { "town_wood_01", "town_mushroom_01" };
        var oldSave = new JavaScriptSerializer().Deserialize<System.Collections.Generic.Dictionary<string, object>>(original);
        oldSave.Remove("acceptedTownQuests");
        oldSave.Remove("townQuestStates");
        File.WriteAllText(savePath, new JavaScriptSerializer().Serialize(oldSave));
        GameSaveData legacy = store.Load(alice.id, 1);
        Check(legacy.acceptedTownQuests == null || legacy.acceptedTownQuests.Length == 0,
            "older saves without town quests remain loadable");
        File.WriteAllText(savePath, original);
        data.health = 35; store.Save(data);
        Check(File.ReadAllText(savePath + ".bak") == original && store.Load(alice.id, 1).health == 35,
            "atomic replacement preserves previous save as backup");
        data.health = 30; store.Save(data);
        Check(store.Load(alice.id, 1).health == 30, "repeated backup replacement succeeds");
        string latest = File.ReadAllText(savePath);
        data.skills[0] = 1;
        Reject(() => store.Save(data), "invalid skill prerequisite rejected");
        Check(File.ReadAllText(savePath) == latest, "validation failure never overwrites save");
        File.WriteAllText(savePath, "{broken json");
        Reject(() => store.Load(alice.id, 1), "corrupt save rejected");
        Check(File.ReadAllText(savePath) == "{broken json", "corrupt save preserved");
        File.WriteAllText(savePath, latest.Replace("\"version\":1", "\"version\":999"));
        Reject(() => store.Load(alice.id, 1), "unsupported save version rejected");
        File.WriteAllText(accountsPath, "{broken json");
        Reject(() => store.Register("Charlie", password), "corrupt account database is not reset");
        Check(File.ReadAllText(accountsPath) == "{broken json", "corrupt account database preserved");
        Console.WriteLine("Passed " + count + " checks. Test artifacts: " + root);
        return 0;
    }
}
