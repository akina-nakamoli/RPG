using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Solid water around the land, without blocking the island itself.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-400)]
public class WaterBoundary : MonoBehaviour
{
    [SerializeField] private Tilemap ground;
    [SerializeField] private Tilemap[] additionalLand;
    [Min(1)] [SerializeField] private int borderCells = 3;
    [SerializeField] private GameObject generatedBoundary;
    private void Awake() => Rebuild();

    [ContextMenu("Rebuild Water Collision")]
    public void Rebuild()
    {
        if (ground == null) { Debug.LogError("WaterBoundary needs Ground.", this); return; }
        var maps = new List<Tilemap> { ground };
        if (additionalLand != null)
            foreach (Tilemap map in additionalLand) if (map != null) maps.Add(map);
        BoundsInt bounds = ground.cellBounds;
        int minX = bounds.xMin, maxX = bounds.xMax, minY = bounds.yMin, maxY = bounds.yMax;
        foreach (Tilemap map in maps)
        {
            if (map.GetUsedTilesCount() == 0) continue;
            Vector3Int first = ground.WorldToCell(map.CellToWorld(map.cellBounds.min));
            Vector3Int last = ground.WorldToCell(map.CellToWorld(map.cellBounds.max));
            minX = Mathf.Min(minX, first.x); maxX = Mathf.Max(maxX, last.x);
            minY = Mathf.Min(minY, first.y); maxY = Mathf.Max(maxY, last.y);
        }
        int padding = Mathf.Max(1, borderCells);
        minX -= padding; minY -= padding; maxX += padding; maxY += padding;
        if ((long)(maxX - minX) * (maxY - minY) > 250000)
        {
            Debug.LogError("Water collision bounds too large. Check outlying map tiles.", this);
            return;
        }
        if (generatedBoundary != null)
        {
            generatedBoundary.SetActive(false);
            if (Application.isPlaying) Destroy(generatedBoundary); else DestroyImmediate(generatedBoundary);
        }
        // Do not inherit the Water sprite's huge scale.
        generatedBoundary = new GameObject("WaterCollision", typeof(Rigidbody2D), typeof(CompositeCollider2D));
        generatedBoundary.transform.SetParent(ground.transform, false);
        generatedBoundary.layer = gameObject.layer;
        generatedBoundary.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
        CompositeCollider2D composite = generatedBoundary.GetComponent<CompositeCollider2D>();
        composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
        composite.generationType = CompositeCollider2D.GenerationType.Manual;
        for (int y = minY; y < maxY; y++)
        {
            int start = minX;
            while (start < maxX)
            {
                if (IsLand(start, y, maps)) { start++; continue; }
                int end = start + 1;
                while (end < maxX && !IsLand(end, y, maps)) end++;
                GameObject row = new GameObject("WaterRow_" + y + "_" + start, typeof(BoxCollider2D));
                row.layer = gameObject.layer;
                row.transform.SetParent(generatedBoundary.transform, false);
                Vector3 lower = ground.CellToLocal(new Vector3Int(start, y, 0));
                Vector3 upper = ground.CellToLocal(new Vector3Int(end, y + 1, 0));
                row.transform.localPosition = (lower + upper) * .5f;
                BoxCollider2D box = row.GetComponent<BoxCollider2D>();
                box.size = new Vector2(Mathf.Abs(upper.x - lower.x), Mathf.Abs(upper.y - lower.y));
                box.usedByComposite = true;
                start = end;
            }
        }
        Physics2D.SyncTransforms();
        composite.GenerateGeometry();
    }

    private bool IsLand(int x, int y, List<Tilemap> maps)
    {
        Vector3 position = ground.GetCellCenterWorld(new Vector3Int(x, y, 0));
        foreach (Tilemap map in maps) if (map.HasTile(map.WorldToCell(position))) return true;
        return false;
    }
}
