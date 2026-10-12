using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>Account-specific local saves and optional authenticated cloud synchronization.</summary>
public sealed class LocalAccountStore
{
    private const int Iterations = 120000;
    private readonly string root;
    private readonly DatabaseAccountApi database;
    public bool UsesDatabase => database != null;
    public string SyncStatus { get; private set; }
    private bool syncing;
    private bool resolving;
    private bool sessionExpired;
    private string activeAccount;
    private readonly Dictionary<int, GameSaveData> cached = new Dictionary<int, GameSaveData>();
    public async Task<LocalAccount> AuthenticateAsync(string username, string password, bool register, string invitationCode = null)
    {
        if (database == null) return register ? Register(username, password) : Login(username, password);
        if (syncing || resolving) throw new InvalidOperationException("Wait for cloud synchronization to finish.");
        LocalAccount account = await database.AuthenticateAsync(username, password, register, invitationCode);
        var loaded = new Dictionary<int, GameSaveData>();
        try
        {
        for (int slot = 1; slot <= 3; slot++)
        {
            string path = SavePath(account.id, slot);
            GameSaveData local = File.Exists(path) ? JsonUtility.FromJson<GameSaveData>(ReadBounded(path)) : null;
            if (local != null) Validate(local, account.id, slot);
            GameSaveData remote = await database.HasSaveAsync(account.id, slot) ? await database.LoadAsync(account.id, slot) : null;
            // An upload can succeed even if its response was lost. Its receipt resolves that ambiguity.
            if (local != null && local.cloudPending && remote != null
                && !string.IsNullOrEmpty(local.cloudRequestId) && local.cloudRequestId == remote.cloudRequestId)
                local = remote;
            GameSaveData chosen = local != null && local.cloudPending ? local : remote;
            if (chosen == null) continue;
            if (chosen.cloudPending && chosen.cloudRevision != (remote != null ? remote.cloudRevision : 0)) chosen.cloudConflict = true;
            Validate(chosen, account.id, slot);
            loaded[slot] = chosen;
        }
        foreach (var entry in loaded)
        {
            string path = SavePath(account.id, entry.Key);
            string incoming = JsonUtility.ToJson(entry.Value, true);
            // Returning from passwordless offline mode must not discard local-only
            // progress when the account's cloud copy replaces its cache. Keep a
            // separate archive; ordinary .bak files are replaced by later saves.
            if (File.Exists(Path.Combine(root, "offline-profile.json")) && File.Exists(path))
            {
                string previous = ReadBounded(path);
                if (previous != incoming)
                    AtomicWrite(path + "." + Guid.NewGuid().ToString("N") + ".offline-backup", previous);
            }
            AtomicWrite(path, incoming);
        }
        }
        catch
        {
            try { await database.LogoutAsync(account.id); } catch (Exception) { }
            activeAccount = null;
            cached.Clear();
            throw;
        }
        activeAccount = account.id;
        sessionExpired = false;
        cached.Clear();
        foreach (var entry in loaded) cached[entry.Key] = entry.Value;
        SyncStatus = "Cloud slots loaded. Pending local saves will retry safely.";
        return account;
    }
    public async Task LogoutAsync()
    {
        if (database == null || activeAccount == null) return;
        while (syncing || resolving) await Task.Delay(50);
        string account = activeAccount;
        try { await database.LogoutAsync(account); }
        catch (Exception) { }
        finally { activeAccount = null; cached.Clear(); }
    }
    public async Task SyncAsync()
    {
        if (database == null || activeAccount == null || syncing || resolving || sessionExpired) return;
        syncing = true;
        try
        {
            foreach (int slot in new List<int>(cached.Keys))
            {
                GameSaveData current = cached[slot];
                if (!current.cloudPending) continue;
                if (current.cloudConflict) { SyncStatus = "CLOUD CONFLICT: choose a copy in the login save-slot screen."; continue; }
                GameSaveData snapshot = JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(current));
                try
                {
                    int revision = await database.SaveAsync(snapshot);
                    GameSaveData latest = cached[slot];
                    latest.cloudRevision = revision;
                    if (latest.cloudRequestId == snapshot.cloudRequestId) latest.cloudPending = false;
                    else latest.cloudRequestId = Guid.NewGuid().ToString("N");
                    AtomicWrite(SavePath(activeAccount, slot), JsonUtility.ToJson(latest, true));
                    SyncStatus = latest.cloudPending ? "Local saved; newer progress awaits cloud sync." : "Local saved and cloud synchronized.";
                }
                catch (DatabaseAccountApi.ApiException ex)
                {
                    if (ex.Status == 409)
                    {
                        cached[slot].cloudConflict = true;
                        AtomicWrite(SavePath(activeAccount, slot), JsonUtility.ToJson(cached[slot], true));
                    }
                    SyncStatus = ex.Status == 409 ? "CLOUD CONFLICT: both copies preserved. Resolve before uploading."
                        : ex.Status == 401 ? "Local saved. Session expired; return to login to synchronize."
                        : "Local saved. Cloud unavailable; synchronization will retry.";
                    if (ex.Status == 401) sessionExpired = true;
                    if (ex.Status == 400 || ex.Status == 403)
                        SyncStatus = "Local saved. Cloud rejected this save; check server compatibility and account permissions.";
                    return;
                }
            }
            foreach (GameSaveData save in cached.Values)
                if (save.cloudConflict) { SyncStatus = "CLOUD CONFLICT: both copies preserved. Choose a copy in the login screen."; break; }
        }
        finally { syncing = false; }
    }
    public bool HasCloudConflict(int slot) => cached.TryGetValue(slot, out GameSaveData data) && data.cloudConflict;
    public async Task ResolveConflictAsync(int slot, bool keepLocal)
    {
        if (!HasCloudConflict(slot) || syncing || resolving) throw new InvalidOperationException("Wait for synchronization to finish.");
        resolving = true;
        try
        {
        GameSaveData local = JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(cached[slot]));
        GameSaveData remote = await database.LoadAsync(activeAccount, slot);
        Validate(remote, activeAccount, slot);
        string path = SavePath(activeAccount, slot);
        AtomicWrite(path + "." + Guid.NewGuid().ToString("N") + (keepLocal ? ".cloud-conflict-backup" : ".local-conflict-backup"), JsonUtility.ToJson(keepLocal ? remote : local, true));
        if (keepLocal)
        {
            local.cloudRevision = remote.cloudRevision;
            local.cloudRequestId = Guid.NewGuid().ToString("N");
            local.cloudConflict = false;
            AtomicWrite(path, JsonUtility.ToJson(local, true));
            cached[slot] = local;
        }
        else { AtomicWrite(path, JsonUtility.ToJson(remote, true)); cached[slot] = remote; }
        SyncStatus = keepLocal ? "Local copy selected; cloud sync pending." : "Cloud copy restored. Previous local copy backed up.";
        }
        finally { resolving = false; }
        if (keepLocal) await SyncAsync();
    }
    public LocalAccountStore(string directory)
    {
        root = directory;
        TextAsset configAsset = Resources.Load<TextAsset>("DatabaseConfig");
        if (configAsset == null) return;
        var config = JsonUtility.FromJson<DatabaseAccountApi.Config>(configAsset.text);
        if (config != null && config.enabled) database = new DatabaseAccountApi(config.apiUrl);
    }


    public LocalAccount Register(string username, string password)
    {
        if (database != null) throw new InvalidOperationException("Use asynchronous authentication.");
        username = (username ?? "").Trim();
        if (!Regex.IsMatch(username, "^[A-Za-z0-9_]{3,20}$"))
            throw new InvalidOperationException("Use 3-20 letters, numbers or underscores for your username.");
        if (password == null || password.Length < 8 || password.Length > 128)
            throw new InvalidOperationException("Use a password with 8-128 characters.");
        LocalAccountDatabase db = ReadAccounts();
        foreach (LocalAccount existing in db.accounts)
            if (string.Equals(existing.username, username, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("That username already exists on this computer.");
        byte[] salt = new byte[16];
        using (RandomNumberGenerator random = RandomNumberGenerator.Create()) random.GetBytes(salt);
        LocalAccount account = new LocalAccount
        {
            id = Guid.NewGuid().ToString("N"), username = username,
            salt = Convert.ToBase64String(salt), iterations = Iterations,
            passwordHash = Convert.ToBase64String(Derive(password, salt, Iterations))
        };
        var accounts = new List<LocalAccount>(db.accounts) { account };
        db.accounts = accounts.ToArray();
        AtomicWrite(Path.Combine(root, "accounts.json"), JsonUtility.ToJson(db, true));
        return account;
    }

    public LocalAccount Login(string username, string password)
    {
        if (database != null) throw new InvalidOperationException("Use asynchronous authentication.");
        if (password == null || password.Length > 128)
            throw new InvalidOperationException("Incorrect username or password.");
        foreach (LocalAccount account in ReadAccounts().accounts)
        {
            if (!string.Equals(account.username, (username ?? "").Trim(), StringComparison.OrdinalIgnoreCase)) continue;
            byte[] expected = Convert.FromBase64String(account.passwordHash);
            byte[] actual = Derive(password, Convert.FromBase64String(account.salt), account.iterations);
            int difference = actual.Length ^ expected.Length;
            for (int i = 0; i < Math.Min(actual.Length, expected.Length); i++) difference |= actual[i] ^ expected[i];
            if (difference == 0) return account;
            break;
        }
        throw new InvalidOperationException("Incorrect username or password.");
    }

    private static byte[] Derive(string password, byte[] salt, int iterations)
    {
        using (var derive = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
            return derive.GetBytes(32);
    }

    private LocalAccountDatabase ReadAccounts()
    {
        string path = Path.Combine(root, "accounts.json");
        if (!File.Exists(path)) return new LocalAccountDatabase();
        var db = JsonUtility.FromJson<LocalAccountDatabase>(ReadBounded(path));
        if (db == null || db.version != 1 || db.accounts == null)
            throw new InvalidDataException("Account data is invalid. Original files were preserved.");
        var ids = new HashSet<string>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (LocalAccount a in db.accounts)
        {
            if (a == null || !Guid.TryParseExact(a.id, "N", out _) || string.IsNullOrEmpty(a.username)
                || !ids.Add(a.id) || !names.Add(a.username) || a.iterations < 10000 || a.iterations > 1000000
                || Convert.FromBase64String(a.salt ?? "").Length != 16
                || Convert.FromBase64String(a.passwordHash ?? "").Length != 32)
                throw new InvalidDataException("Account data is invalid. Original files were preserved.");
        }
        return db;
    }

    public bool HasSave(string accountId, int slot) => database != null ? accountId == activeAccount && cached.ContainsKey(slot) : File.Exists(SavePath(accountId, slot));
    public async Task DeleteSaveAsync(string accountId, int slot)
    {
        string path = SavePath(accountId, slot); // validates exact account and slot
        if (syncing || resolving) throw new InvalidOperationException("Wait for synchronization to finish, then retry deletion.");
        if (database != null && accountId != activeAccount) throw new InvalidOperationException("Account access denied.");
        resolving = true;
        try
        {
            if (database != null)
            {
                if (!cached.TryGetValue(slot, out GameSaveData save)) throw new InvalidOperationException("This slot is empty.");
                await database.DeleteSaveAsync(accountId, slot, save.cloudRevision);
            }
            // Preserve a recoverable copy, outside the active slot filename.
            string suffix = ".deleted-" + Guid.NewGuid().ToString("N");
            if (File.Exists(path)) File.Move(path, path + suffix);
            if (File.Exists(path + ".bak")) File.Move(path + ".bak", path + suffix + ".bak");
            cached.Remove(slot);
            SyncStatus = "Save slot deleted. A recovery backup was preserved.";
        }
        finally { resolving = false; }
    }

    public GameSaveData Load(string accountId, int slot)
    {
        if (database != null)
        {
            if (accountId != activeAccount || !cached.TryGetValue(slot, out GameSaveData remote))
                throw new InvalidOperationException("This cloud slot is empty or not authenticated.");
            Validate(remote, accountId, slot);
            return remote;
        }
        string path = SavePath(accountId, slot);
        if (!File.Exists(path)) throw new InvalidOperationException("This slot is empty.");
        var data = JsonUtility.FromJson<GameSaveData>(ReadBounded(path));
        Validate(data, accountId, slot);
        return data;
    }

    public void Save(GameSaveData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        Validate(data, data.accountId, data.slot);
        if (database != null)
        {
            if (data.accountId != activeAccount) throw new InvalidOperationException("Account access denied.");
            data.cloudRevision = cached.TryGetValue(data.slot, out GameSaveData previous) ? previous.cloudRevision : 0;
            data.cloudPending = true;
            data.cloudConflict = previous != null && previous.cloudConflict;
            data.cloudRequestId = Guid.NewGuid().ToString("N");
            AtomicWrite(SavePath(data.accountId, data.slot), JsonUtility.ToJson(data, true));
            cached[data.slot] = data;
            SyncStatus = sessionExpired ? "Local saved. Session expired; sign out and log in again to synchronize."
                : data.cloudConflict ? "Local saved. Cloud conflict requires a choice in the login screen."
                : "Local saved. Cloud sync pending.";
            return;
        }
        AtomicWrite(SavePath(data.accountId, data.slot), JsonUtility.ToJson(data, true));
    }

    private string SavePath(string accountId, int slot)
    {
        if (!Guid.TryParseExact(accountId, "N", out _) || slot < 1 || slot > 3)
            throw new InvalidOperationException("Invalid account or save slot.");
        return Path.Combine(root, accountId, "slot-" + slot + ".json");
    }

    private static void Validate(GameSaveData d, string accountId, int slot)
    {
        if (d == null || d.version != 1 || d.accountId != accountId || d.slot != slot
            || (d.scene != "SampleScene" && d.scene != "WildernessForest")
            || !Guid.TryParseExact(d.accountId, "N", out _) || slot < 1 || slot > 3
            || !Finite(d.x) || !Finite(d.y) || !Finite(d.z) || d.health < 1 || d.health > 1000000
            || d.level < 0 || d.level > 100000 || d.experience < 0 || d.skillPoints < 0
            || d.skills == null || d.skills.Length != 15 || d.items == null || d.items.Length > 1000
            || d.completedGreetings == null || d.terrainColliders == null)
            throw new InvalidDataException("Invalid or unsupported save. Original files were preserved.");
        for (int c = 0; c < 5; c++) for (int r = 0; r < 3; r++)
        {
            int rank = d.skills[c * 3 + r];
            if (rank < 0 || rank > 5 || (r > 0 && rank > 0 && d.skills[c * 3 + r - 1] < 5))
                throw new InvalidDataException("Invalid skill data.");
        }
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (d.targetRangeBestScore < 0 || d.targetRangeBestScore > 1000000) throw new InvalidDataException("Invalid target range score.");
            if (d.archerSelected && !d.targetRangeHeroUnlocked) throw new InvalidDataException("Selected hero is locked.");
            if (d.escortJameStage < 0 || d.escortJameStage > 4) throw new InvalidDataException("Invalid escort stage.");
          if (d.escortJameHealth < 0 || d.escortJameHealth > 10) throw new InvalidDataException("Invalid escort health.");
          if (d.defeatedForestGoblins != null)
          {
              if (d.defeatedForestGoblins.Length > 1000) throw new InvalidDataException("Too many forest enemies.");
              var ids = new HashSet<string>();
              foreach (string id in d.defeatedForestGoblins)
                  if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || !ids.Add(id)) throw new InvalidDataException("Invalid forest enemy state.");
          }
        // Missing field is valid for saves made before the town task board existed.
        if (d.acceptedTownQuests != null)
        {
            if (d.acceptedTownQuests.Length > 1000) throw new InvalidDataException("Too many accepted quests.");
            var questIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in d.acceptedTownQuests)
                if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || !questIds.Add(id))
                    throw new InvalidDataException("Invalid accepted quest data.");
        }
        foreach (SavedItem item in d.items)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.key) || item.amount < 0 || !keys.Add(item.key))
                throw new InvalidDataException("Invalid inventory data.");
        }
        if (d.townQuestStates != null)
        {
            if (d.townQuestStates.Length > 1000) throw new InvalidDataException("Too many quest states.");
            var stateIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (SavedTownQuest state in d.townQuestStates)
            {
                if (state == null || string.IsNullOrWhiteSpace(state.id) || state.id.Length > 128
                    || !stateIds.Add(state.id) || state.progress < 0 || state.progress > 100000)
                    throw new InvalidDataException("Invalid quest progress data.");
                int target = TownQuestProgress.Target(state.id);
                if (target > 0 && (state.progress > target || (state.completed && state.progress != target)))
                    throw new InvalidDataException("Invalid completed quest data.");
            }
        }
        foreach (SavedCollider item in d.terrainColliders)
            if (item == null || string.IsNullOrEmpty(item.key)) throw new InvalidDataException("Invalid terrain data.");
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static string ReadBounded(string path)
    {
        if (new FileInfo(path).Length > 4 * 1024 * 1024) throw new InvalidDataException("Data file is too large.");
        return File.ReadAllText(path);
    }

    private static void AtomicWrite(string path, string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temp = path + ".tmp";
        using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(file)) { writer.Write(json); writer.Flush(); file.Flush(true); }
        // Keep the previous version for manual recovery. Never delete the old file before replacing it.
        if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
        else File.Move(temp, path);
    }
}
