using System;

[Serializable]
public class GameSaveData
{
    public int version = 1;
    public string[] defeatedForestGoblins = new string[0];
    public bool forestChestOpened;
    public int escortJameStage;
    public int escortJameHealth = 10;
    public int targetRangeBestScore;
    public bool targetRangeHeroUnlocked;
    public bool archerSelected;
    public int cloudRevision;
    public bool cloudPending;
    public bool cloudConflict;
    public string cloudRequestId;
    public string accountId;
    public int slot;
    public string savedUtc;
    public string scene = "SampleScene";
    public float x, y, z;
    public int health, level, experience, skillPoints, sortingOrder;
    public int[] skills = new int[15];
    public SavedItem[] items = new SavedItem[0];
    public string[] completedGreetings = new string[0];
    public string[] acceptedTownQuests = new string[0];
    public SavedTownQuest[] townQuestStates = new SavedTownQuest[0];
    public SavedCollider[] terrainColliders = new SavedCollider[0];
}

[Serializable]
public class SavedTownQuest
{
    public string id;
    public int progress;
    public bool completed;
}

[Serializable]
public class SavedItem
{
    public string key;
    public int amount;
}

[Serializable]
public class SavedCollider
{
    public string key;
    public bool enabled;
}

[Serializable]
public class LocalAccount
{
    public string id;
    public string username;
    public string salt;
    public string passwordHash;
    public int iterations;
}

[Serializable]
public class LocalAccountDatabase
{
    public int version = 1;
    public LocalAccount[] accounts = new LocalAccount[0];
}
