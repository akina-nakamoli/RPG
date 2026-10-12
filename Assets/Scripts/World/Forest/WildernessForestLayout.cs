using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Deterministic maze: scene travel and saves retain identical routes.</summary>
[DefaultExecutionOrder(-500)]
public class WildernessForestLayout : MonoBehaviour
{
    public GameObject treePrefab;
    public GameObject goblinPrefab;
    public ItemSO chestGold;
    // Top-left, top, top-right, left, center, right, bottom-left, bottom, bottom-right.
    [SerializeField] private TileBase[] roadTiles = new TileBase[9];
    [SerializeField] private bool built;
    [SerializeField] private int layoutVersion;
    [SerializeField, Range(5, 25)] private int mazeColumns = 13, mazeRows = 11;
    [SerializeField] private int mazeSeed = 4271;
    [SerializeField, Range(2f, 8f)] private float clearViewRadius = 4.5f;
    public bool NeedsRebuild => !built || layoutVersion < 4;
    private const int CellSize = 3;
    private readonly Vector2 origin = new Vector2(-15.5f, -4.5f);
    private void Awake() { if (NeedsRebuild) BuildLayout(); }

    [ContextMenu("Rebuild Forest Maze")]
    public void RebuildLayout() { built = false; BuildLayout(); }

    [ContextMenu("Build Forest Maze")]
    public void BuildLayout()
    {
        if (!NeedsRebuild) return;
        if (layoutVersion < 3) { mazeColumns = 11; mazeRows = 9; }
#if UNITY_EDITOR
        if (goblinPrefab == null) goblinPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/Enemy_1.prefab");
        if (chestGold == null) chestGold = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemSO>("Assets/Data/Items/Gold.asset");
        int[] roadIndices = { 5, 6, 7, 14, 15, 16, 22, 23, 24 };
        roadTiles = new TileBase[9];
        for (int i = 0; i < roadIndices.Length; i++) roadTiles[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Gress Tlies/Tilemap_Flat_" + roadIndices[i] + ".asset");
#endif
        TileBase grass = null, road = null;
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            foreach (Tilemap map in root.GetComponentsInChildren<Tilemap>(true))
            {
                if (map.name == "Ground") grass = MostCommonTile(map);
                if (map.name == "ForestPath" || map.name == "Tilemap") road = MostCommonTile(map);
            }
        if (grass == null || treePrefab == null) { Debug.LogError("Forest maze needs Ground tiles and Tree prefab.", this); return; }
        var oldChildren = new List<GameObject>();
        foreach (Transform child in transform) oldChildren.Add(child.gameObject);
        foreach (GameObject child in oldChildren) Remove(child);
        transform.position = Vector3.zero; transform.rotation = Quaternion.identity; transform.localScale = Vector3.one;
        int columns = Mathf.Clamp(mazeColumns, 5, 25), rows = Mathf.Clamp(mazeRows, 5, 25);
        int width = columns * 2 + 1, height = rows * 2 + 1;
        bool[,] passages = GenerateMaze(columns, rows, mazeSeed);
        Vector2Int exit = FindExit(passages);
        Vector2Int gate = exit.x == width - 2 ? exit + Vector2Int.right : exit + Vector2Int.up;
        passages[0, 1] = passages[gate.x, gate.y] = true;
        Transform grid = Child("ForestGrid", transform, typeof(Grid)).transform; grid.position = origin;
        Tilemap ground = Child("Ground", grid, typeof(Tilemap), typeof(TilemapRenderer)).GetComponent<Tilemap>();
        ground.GetComponent<TilemapRenderer>().sortingOrder = -2;
        Tilemap path = Child("ForestPath", grid, typeof(Tilemap), typeof(TilemapRenderer)).GetComponent<Tilemap>();
        path.GetComponent<TilemapRenderer>().sortingOrder = -1;
        for (int x = 0; x < width * CellSize; x++) for (int y = 0; y < height * CellSize; y++)
        {
            Vector3Int cell = new Vector3Int(x, y, 0); ground.SetTile(cell, grass);
            if (passages[x / CellSize, y / CellSize])
            {
                int index = RoadTileIndex(passages, x, y);
                TileBase selected = roadTiles != null && roadTiles.Length == 9 ? roadTiles[index] : null;
                if (selected != null || road != null) path.SetTile(cell, selected != null ? selected : road);
            }
        }
        Transform walls = Child("UnchoppableForestWalls", transform).transform;
        var random = new System.Random(mazeSeed);
        for (int x = 0; x < width; x++) for (int y = 0; y < height; y++)
        {
            if (passages[x, y]) continue;
            Vector2 center = Center(x, y);
            BoxCollider2D collider = Child("TreeWall_" + x + "_" + y, walls, typeof(BoxCollider2D)).GetComponent<BoxCollider2D>();
            collider.transform.position = center; collider.size = Vector2.one * CellSize;
            for (int i = 0; i < 2; i++)
            {
                Vector2 root = center + new Vector2(i == 0 ? -.7f : .7f, ((float)random.NextDouble() - .5f) * .6f);
                GameObject tree;
#if UNITY_EDITOR
                if (!Application.isPlaying) tree = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(treePrefab, collider.transform);
                else
#endif
                    tree = Instantiate(treePrefab, collider.transform);
                TreeBehaviour behaviour = tree.GetComponent<TreeBehaviour>();
                if (behaviour != null) { behaviour.canBeChopped = false; tree.transform.position = root - behaviour.rootOffset; }
                else tree.transform.position = root;
                // Continuous cell colliders prevent squeezing between trunks.
                foreach (Collider2D trunk in tree.GetComponentsInChildren<Collider2D>()) trunk.enabled = false;
            }
        }
        Vector2 size = new Vector2(width * CellSize, height * CellSize);
        Boundary("NorthBoundary", origin + new Vector2(size.x / 2, size.y), new Vector2(size.x, .4f));
        Boundary("SouthBoundary", origin + new Vector2(size.x / 2, 0), new Vector2(size.x, .4f));
        Boundary("WestBoundary", origin + new Vector2(0, size.y / 2), new Vector2(.4f, size.y));
        Boundary("EastBoundary", origin + new Vector2(size.x, size.y / 2), new Vector2(.4f, size.y));
        MakePortal("ReturnToVillagePortal", Center(0, 1), "Return to the village?", false);
        MakePortal("MazeExitPortal", Center(gate.x, gate.y), "You found the forest maze exit! Return to the village?", true);
        var deadEnds = new List<Vector2Int>();
        Vector2Int[] directions = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
        for (int x = 1; x < width - 1; x += 2) for (int y = 1; y < height - 1; y += 2)
        {
            var cell = new Vector2Int(x, y);
            if (cell == new Vector2Int(1, 1) || cell == exit) continue;
            int neighbors = 0;
            foreach (var direction in directions) if (passages[x + direction.x, y + direction.y]) neighbors++;
            if (neighbors == 1) deadEnds.Add(cell);
        }
        for (int i = deadEnds.Count - 1; i > 0; i--) { int j = random.Next(i + 1); var cell = deadEnds[i]; deadEnds[i] = deadEnds[j]; deadEnds[j] = cell; }
        Transform encounters = Child("DeadEndEncounters", transform).transform;
        for (int i = 0; i < Mathf.Min(8, deadEnds.Count - 1); i++)
        {
            if (goblinPrefab == null) break;
            GameObject enemy = Instantiate(goblinPrefab, encounters);
            enemy.name = "ForestGoblin_" + deadEnds[i].x + "_" + deadEnds[i].y;
            enemy.transform.position = Center(deadEnds[i].x, deadEnds[i].y);
            enemy.AddComponent<ForestGoblinIdentity>().id = "forest3-" + mazeSeed + "-" + deadEnds[i].x + "-" + deadEnds[i].y;
        }
        if (deadEnds.Count > 0)
        {
            var cell = deadEnds[deadEnds.Count - 1];
            var chest = Child("ForestTreasureChest", encounters, typeof(ForestChest)).GetComponent<ForestChest>();
            chest.transform.position = Center(cell.x, cell.y); chest.gold = chestGold;
        }
        Camera camera = null;
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            foreach (Camera candidate in root.GetComponentsInChildren<Camera>(true))
                if (candidate.enabled && candidate.gameObject.activeInHierarchy) { camera = candidate; break; }
        if (camera == null) camera = Child("ForestCamera", transform, typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 7f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.035f, .07f, .065f); camera.cullingMask = -1;
        camera.transform.position = new Vector3(-11, 0, -10);
        var view = camera.GetComponent<ForestMazeView>(); if (view == null) view = camera.gameObject.AddComponent<ForestMazeView>();
        view.minimum = origin; view.maximum = origin + size; view.clearRadius = clearViewRadius; view.fogRoot = transform;
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            PlayerMovements player = root.GetComponentInChildren<PlayerMovements>();
            if (player != null) player.TeleportTo(new Vector2(-11, 0));
        }
        built = true; layoutVersion = 4; Physics2D.SyncTransforms();
    }
    public static int RoadTileIndex(bool[,] passages, int x, int y)
    {
        // Test fine tile coordinates, not logical maze cells: neighboring road
        // blocks must join seamlessly across every three-tile cell boundary.
        bool left = IsRoad(passages, x - 1, y), right = IsRoad(passages, x + 1, y);
        bool top = IsRoad(passages, x, y + 1), bottom = IsRoad(passages, x, y - 1);
        int column = !left ? 0 : !right ? 2 : 1;
        int row = !top ? 0 : !bottom ? 2 : 1;
        return row * 3 + column;
    }
    private static bool IsRoad(bool[,] passages, int x, int y)
    {
        return x >= 0 && y >= 0 && x < passages.GetLength(0) * CellSize && y < passages.GetLength(1) * CellSize
            && passages[x / CellSize, y / CellSize];
    }
    public static bool[,] GenerateMaze(int columns, int rows, int seed)
    {
        bool[,] open = new bool[columns * 2 + 1, rows * 2 + 1];
        var random = new System.Random(seed); var stack = new List<Vector2Int>();
        Vector2Int[] steps = { Vector2Int.right * 2, Vector2Int.up * 2, Vector2Int.left * 2, Vector2Int.down * 2 };
        stack.Add(new Vector2Int(1, 1)); open[1, 1] = true;
        while (stack.Count > 0)
        {
            int index = random.Next(3) == 0 ? random.Next(stack.Count) : stack.Count - 1;
            Vector2Int current = stack[index]; var options = new List<Vector2Int>();
            foreach (Vector2Int step in steps)
            {
                Vector2Int next = current + step;
                if (next.x > 0 && next.y > 0 && next.x < open.GetLength(0) - 1 && next.y < open.GetLength(1) - 1 && !open[next.x, next.y]) options.Add(next);
            }
            if (options.Count == 0) { stack.RemoveAt(index); continue; }
            Vector2Int chosen = options[random.Next(options.Count)], middle = (chosen + current) / 2;
            open[middle.x, middle.y] = open[chosen.x, chosen.y] = true; stack.Add(chosen);
        }
        return open;
    }
    public static Vector2Int FindExit(bool[,] open)
    {
        var visited = new HashSet<Vector2Int>(); var queue = new Queue<Vector2Int>();
        Vector2Int best = new Vector2Int(1, 1); queue.Enqueue(best); visited.Add(best);
        Vector2Int[] steps = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
        while (queue.Count > 0)
        {
            Vector2Int cell = queue.Dequeue();
            if (cell.x == open.GetLength(0) - 2 || cell.y == open.GetLength(1) - 2) best = cell;
            foreach (Vector2Int step in steps)
            {
                Vector2Int next = cell + step;
                if (next.x < 0 || next.y < 0 || next.x >= open.GetLength(0) || next.y >= open.GetLength(1) || !open[next.x, next.y] || !visited.Add(next)) continue;
                queue.Enqueue(next);
            }
        }
        return best;
    }
    private Vector2 Center(int x, int y) => origin + new Vector2((x + .5f) * CellSize, (y + .5f) * CellSize);
    private void Boundary(string name, Vector2 position, Vector2 size)
    {
        BoxCollider2D wall = Child(name, transform, typeof(BoxCollider2D)).GetComponent<BoxCollider2D>(); wall.transform.position = position; wall.size = size;
    }
    private void MakePortal(string name, Vector2 position, string message, bool exit)
    {
        ForestPortal portal = Child(name, transform, typeof(ForestPortal)).GetComponent<ForestPortal>();
        portal.transform.position = position; portal.destinationScene = "SampleScene"; portal.arrivalPosition = new Vector2(10.5f, -8.6f);
        portal.regionSize = new Vector2(2.2f, 2.2f); portal.message = message;
    }
    private static TileBase MostCommonTile(Tilemap map)
    {
        var counts = new Dictionary<TileBase, int>(); TileBase best = null; int highest = 0;
        foreach (Vector3Int cell in map.cellBounds.allPositionsWithin)
        {
            TileBase tile = map.GetTile(cell); if (tile == null) continue;
            counts.TryGetValue(tile, out int count); counts[tile] = ++count;
            if (count > highest) { highest = count; best = tile; }
        }
        return best;
    }
    private static GameObject Child(string name, Transform parent, params System.Type[] components)
    {
        GameObject result = new GameObject(name, components); result.transform.SetParent(parent, false); return result;
    }
    private static void Remove(GameObject target)
    {
        target.SetActive(false); if (Application.isPlaying) Destroy(target); else DestroyImmediate(target);
    }
}
