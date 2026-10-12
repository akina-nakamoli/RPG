using UnityEngine;

/// <summary>Resources holds only references; all prefab assets live under Assets/Prefabs.</summary>
public class GamePrefabCatalog : ScriptableObject
{
    public GameObject targetRangePanel;
    public GameObject escortVillager;
    public GameObject deathPanel;
    public static GamePrefabCatalog Load() => Resources.Load<GamePrefabCatalog>("GamePrefabs");
}
