using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Spawns goblins at random open ground cells in the upper-right map area.</summary>
public sealed class GoblinSpawner : MonoBehaviour
{
    public static GoblinSpawner Instance { get; private set; }

    [SerializeField] private GameObject goblinPrefab;
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Tilemap pathLowTilemap;
    [SerializeField] private Tilemap pathHighTilemap;
    [Min(1)] [SerializeField] private int spawnCount = 5;
    [SerializeField] private Vector2 areaMin = new Vector2(0.68f, 0.68f);
    [SerializeField] private Vector2 areaMax = new Vector2(0.94f, 0.94f);
    [Min(0.1f)] [SerializeField] private float minimumEnemySpacing = 2f;
    [Min(0.05f)] [SerializeField] private float clearRadius = 0.3f;
    [Min(0.1f)] [SerializeField] private float minimumMushroomSpacing = 3f;
    [Min(0.1f)] [SerializeField] private float minimumMushroomEnemySpacing = 2f;
    [Min(1f)] [SerializeField] private float mushroomSearchRadius = 7f;
    private readonly List<Collider2D> overlapResults = new List<Collider2D>();
    private readonly List<Vector2> safeMushroomPositions = new List<Vector2>();
    private ContactFilter2D obstacleFilter;

    private void Awake()
    {
        if (Instance == null || Instance == this) Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (goblinPrefab == null || groundTilemap == null)
        {
            Debug.LogError("GoblinSpawner needs a goblin prefab and the map Ground Tilemap.", this);
            return;
        }

        Physics2D.SyncTransforms();
        obstacleFilter = new ContactFilter2D
        {
            useTriggers = false,
            useLayerMask = false,
            useDepth = false
        };

        BuildSafeMushroomPositions();
        ScatterExistingMushrooms();

        BoundsInt map = groundTilemap.cellBounds;
        int minX = Mathf.Clamp(Mathf.FloorToInt(map.xMin + map.size.x * areaMin.x), map.xMin, map.xMax - 1);
        int maxX = Mathf.Clamp(Mathf.CeilToInt(map.xMin + map.size.x * areaMax.x), minX + 1, map.xMax);
        int minY = Mathf.Clamp(Mathf.FloorToInt(map.yMin + map.size.y * areaMin.y), map.yMin, map.yMax - 1);
        int maxY = Mathf.Clamp(Mathf.CeilToInt(map.yMin + map.size.y * areaMax.y), minY + 1, map.yMax);

        // Build the candidate pool once. Random retries can repeatedly hit empty
        // cells or tree trunks in a forest-heavy corner and accidentally spawn none.
        var candidates = new List<Vector2>();
        var mushroomPositions = new List<Vector2>();
        foreach (Loot loot in FindObjectsOfType<Loot>())
        {
            if (IsMushroom(loot, null)) mushroomPositions.Add(loot.transform.position);
        }
        float mushroomClearanceSquared = minimumMushroomEnemySpacing * minimumMushroomEnemySpacing;
        for (int y = minY; y < maxY; y++)
        for (int x = minX; x < maxX; x++)
        {
            var cell = new Vector3Int(x, y, 0);
            if (!groundTilemap.HasTile(cell)) continue;

            Vector2 point = groundTilemap.GetCellCenterWorld(cell);
            if (HasSolidObstacle(point)
                || IsNearAny(point, mushroomPositions, mushroomClearanceSquared)) continue;
            candidates.Add(point);
        }

        // Fisher-Yates shuffle: each valid grass cell gets an equal chance.
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            (candidates[i], candidates[swapIndex]) = (candidates[swapIndex], candidates[i]);
        }

        var spawnedPositions = new List<Vector2>();
        int desiredCount = Mathf.Max(0, spawnCount);
        if (desiredCount == 0) return;
        foreach (Vector2 point in candidates)
        {
            bool tooClose = false;
            foreach (Vector2 previous in spawnedPositions)
            {
                if ((previous - point).sqrMagnitude < minimumEnemySpacing * minimumEnemySpacing)
                { tooClose = true; break; }
            }
            if (tooClose) continue;

            Vector3 world = point;
            world.z = goblinPrefab.transform.position.z;
            Instantiate(goblinPrefab, world, Quaternion.identity);
            spawnedPositions.Add(point);
            if (spawnedPositions.Count >= desiredCount) break;
        }

        Debug.Log("GoblinSpawner placed " + spawnedPositions.Count + " of " + desiredCount
            + " goblins in the upper-right map area.", this);
        if (spawnedPositions.Count < desiredCount)
            Debug.LogWarning("GoblinSpawner could not place all goblins (" + candidates.Count
                + " clear grass cells before spacing checks). Expand the area or reduce minimum spacing.", this);
    }

    private bool HasSolidObstacle(Vector2 point)
    {
        overlapResults.Clear();
        Physics2D.OverlapCircle(point, clearRadius, obstacleFilter, overlapResults);
        return overlapResults.Count > 0;
    }

    private void BuildSafeMushroomPositions()
    {
        safeMushroomPositions.Clear();
        BoundsInt bounds = groundTilemap.cellBounds;
        for (int y = bounds.yMin; y < bounds.yMax; y++)
        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            var cell = new Vector3Int(x, y, 0);
            if (!groundTilemap.HasTile(cell) || IsOnOrBesidePath(cell)) continue;

            Vector2 point = groundTilemap.GetCellCenterWorld(cell);
            if (!HasSolidObstacle(point)) safeMushroomPositions.Add(point);
        }
    }

    private void ScatterExistingMushrooms()
    {
        Loot[] sceneLoot = FindObjectsOfType<Loot>();
        var mushrooms = new List<Loot>();
        foreach (Loot loot in sceneLoot)
        {
            if (!IsMushroom(loot, null) || loot.IsDeathDrop) continue;
            mushrooms.Add(loot);
        }
        if (mushrooms.Count == 0 || safeMushroomPositions.Count == 0) return;

        // Keep valid, designer-placed pickups where they are. Only repair those
        // on/too close to roads, obstacles, enemies, or another mushroom.
        mushrooms.Sort((a, b) =>
        {
            int xOrder = a.transform.position.x.CompareTo(b.transform.position.x);
            return xOrder != 0 ? xOrder : a.transform.position.y.CompareTo(b.transform.position.y);
        });

        var placed = new List<Vector2>();
        var needsPlacement = new List<Loot>();
        List<Vector2> livingEnemyPositions = GetLivingEnemyPositions();
        float spacingSquared = minimumMushroomSpacing * minimumMushroomSpacing;
        foreach (Loot mushroom in mushrooms)
        {
            Vector2 current = mushroom.transform.position;
            if (IsValidMushroomPosition(current, livingEnemyPositions)
                && IsFarEnough(current, placed, spacingSquared))
            {
                placed.Add(current);
                continue;
            }
            needsPlacement.Add(mushroom);
        }

        foreach (Loot mushroom in needsPlacement)
        {
            Vector2 originalPosition = mushroom.transform.position;
            var nearbyCandidates = new List<Vector2>();
            var allCandidates = new List<Vector2>();
            foreach (Vector2 point in safeMushroomPositions)
            {
                if (!IsValidMushroomPosition(point, livingEnemyPositions)
                    || !IsFarEnough(point, placed, spacingSquared)) continue;

                allCandidates.Add(point);
                if ((point - originalPosition).sqrMagnitude <= mushroomSearchRadius * mushroomSearchRadius)
                    nearbyCandidates.Add(point);
            }

            List<Vector2> candidates = nearbyCandidates.Count > 0 ? nearbyCandidates : allCandidates;
            if (candidates.Count == 0)
            {
                Debug.LogWarning("Could not safely reposition a mushroom; no open grass cell has enough spacing from roads, obstacles, enemies, and other mushrooms.", mushroom);
                continue;
            }

            Vector2 pointToUse = candidates[Random.Range(0, candidates.Count)];
            mushroom.transform.position = new Vector3(pointToUse.x, pointToUse.y, 0f);
            placed.Add(pointToUse);
        }
        Physics2D.SyncTransforms();
    }

    private bool IsValidMushroomPosition(Vector2 point, List<Vector2> livingEnemyPositions)
    {
        if (groundTilemap == null) return false;
        Vector3Int groundCell = groundTilemap.WorldToCell(point);
        return groundTilemap.HasTile(groundCell)
            && !IsOnOrBesidePath(point)
            && !HasSolidObstacle(point)
            && !IsNearAny(point, livingEnemyPositions,
                minimumMushroomEnemySpacing * minimumMushroomEnemySpacing);
    }

    private static List<Vector2> GetLivingEnemyPositions()
    {
        var positions = new List<Vector2>();
        Enemy_Health[] enemies = FindObjectsOfType<Enemy_Health>();
        foreach (Enemy_Health enemy in enemies)
        {
            if (enemy == null || enemy.IsDead) continue;
            positions.Add(enemy.transform.position);
        }
        return positions;
    }

    private static bool IsNearAny(Vector2 point, List<Vector2> others, float minimumDistanceSquared)
    {
        foreach (Vector2 other in others)
        {
            if ((other - point).sqrMagnitude < minimumDistanceSquared)
                return true;
        }
        return false;
    }

    private static bool IsFarEnough(Vector2 point, List<Vector2> others, float minimumDistanceSquared)
    {
        foreach (Vector2 other in others)
        {
            if ((other - point).sqrMagnitude < minimumDistanceSquared)
                return false;
        }
        return true;
    }

    private static bool IsMushroom(Loot loot, Loot mushroomPrefab)
    {
        if (loot == null || loot.ItemDefinition == null) return false;
        if (mushroomPrefab != null && mushroomPrefab.ItemDefinition != null)
            return loot.ItemDefinition == mushroomPrefab.ItemDefinition;
        return string.Equals(loot.ItemDefinition.itemName, "Mushroom", System.StringComparison.OrdinalIgnoreCase);
    }

    private bool IsOnOrBesidePath(Vector3Int groundCell)
    {
        Vector3 worldPosition = groundTilemap.GetCellCenterWorld(groundCell);
        return IsOnOrBesidePath(worldPosition);
    }

    private bool IsOnOrBesidePath(Vector3 worldPosition)
    {
        // Keep pickups off the road itself and one tile away so their sprite does
        // not visually spill onto the path. Convert through each Tilemap's own
        // transform; ground and road cell coordinates are not assumed identical.
        return IsOnOrBesidePath(pathLowTilemap, worldPosition)
            || IsOnOrBesidePath(pathHighTilemap, worldPosition);
    }

    private static bool IsOnOrBesidePath(Tilemap pathTilemap, Vector3 worldPosition)
    {
        if (pathTilemap == null) return false;
        Vector3Int pathCell = pathTilemap.WorldToCell(worldPosition);
        for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++)
        {
            if (pathTilemap.HasTile(pathCell + new Vector3Int(x, y, 0))) return true;
        }
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundTilemap == null) return;
        BoundsInt map = groundTilemap.cellBounds;
        float x0 = map.xMin + map.size.x * areaMin.x;
        float x1 = map.xMin + map.size.x * areaMax.x;
        float y0 = map.yMin + map.size.y * areaMin.y;
        float y1 = map.yMin + map.size.y * areaMax.y;
        Vector3 bottomLeft = groundTilemap.CellToWorld(new Vector3Int(Mathf.RoundToInt(x0), Mathf.RoundToInt(y0), 0));
        Vector3 topRight = groundTilemap.CellToWorld(new Vector3Int(Mathf.RoundToInt(x1), Mathf.RoundToInt(y1), 0));
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube((bottomLeft + topRight) * 0.5f, topRight - bottomLeft);
    }
}
