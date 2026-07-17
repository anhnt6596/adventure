using UnityEngine;

// Terrain is walkability, not buildability - BuildGrid owns the latter. A path around a house is
// walkable and unbuildable; water is neither.
[ExecuteAlways]
public class TerrainGrid : MonoBehaviour
{
    [SerializeField] TerrainSet set;
    [SerializeField, Min(1)] int width = 64;
    [SerializeField, Min(1)] int height = 64;
    [SerializeField, Min(0.01f)] float cellSize = 1f;

    [SerializeField, HideInInspector] byte[] cells;

    TerrainMap _map;
    bool[] _walkable;

    public TerrainSet Set => set;
    public int Width => width;
    public int Height => height;
    public float CellSize => cellSize;

    public TerrainMap Map
    {
        get
        {
            // An undo swaps in a fresh cells array, so identity has to be checked too - otherwise
            // the map keeps writing into the array Unity just discarded.
            if (_map == null || _map.Width != width || _map.Height != height
                || !ReferenceEquals(_map.Cells, cells))
                Rebuild();
            return _map;
        }
    }

    [SerializeField, HideInInspector] int builtWidth;
    [SerializeField, HideInInspector] int builtHeight;

    void Rebuild()
    {
        if (cells == null || cells.Length != width * height)
        {
            // Resizing keeps whatever still fits: losing a painted map to a stray Inspector drag
            // is not an acceptable failure.
            var resized = new byte[width * height];
            if (cells != null && builtWidth > 0)
            {
                int copyW = Mathf.Min(builtWidth, width);
                int copyH = Mathf.Min(builtHeight, height);
                for (int y = 0; y < copyH; y++)
                    for (int x = 0; x < copyW; x++)
                        resized[y * width + x] = cells[y * builtWidth + x];
            }
            cells = resized;
        }

        builtWidth = width;
        builtHeight = height;
        _map = new TerrainMap(width, height, cells);
        _walkable = set != null ? set.BuildWalkableTable() : null;
    }

    public void MarkDirty()
    {
        _walkable = set != null ? set.BuildWalkableTable() : null;
    }

    public bool IsWalkable(int x, int y)
    {
        var map = Map;
        if (!map.InBounds(x, y)) return false;
        if (_walkable == null) return true;
        return _walkable[map.Get(x, y)];
    }

    public bool CanPass(int passMask, int x, int y)
    {
        var map = Map;
        if (!map.InBounds(x, y)) return false;
        return (passMask & TerrainSet.BitOf(map.Get(x, y))) != 0;
    }

    public int DefaultPassMask => set != null ? set.BuildDefaultPassMask() : ~0;

    public byte TerrainAt(int x, int y) => Map.Get(x, y);

    public bool WorldToCell(Vector3 world, out int x, out int y)
    {
        Vector3 local = transform.InverseTransformPoint(world);
        x = Mathf.FloorToInt(local.x / cellSize);
        y = Mathf.FloorToInt(local.z / cellSize);
        return Map.InBounds(x, y);
    }

    public Vector3 CellToWorld(int x, int y)
        => transform.TransformPoint(new Vector3((x + 0.5f) * cellSize, 0f, (y + 0.5f) * cellSize));

#if UNITY_EDITOR
    public enum GizmoMode { Off, Terrain, Unwalkable }

    [SerializeField] GizmoMode gizmos = GizmoMode.Terrain;
    [SerializeField, Range(0f, 1f)] float gizmoAlpha = 0.5f;

    void OnDrawGizmosSelected()
    {
        if (gizmos == GizmoMode.Off || set == null) return;

        var size = new Vector3(cellSize, 0.001f, cellSize);
        var map = Map;

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                Color color;
                if (gizmos == GizmoMode.Unwalkable)
                {
                    if (IsWalkable(x, y)) continue;
                    color = Color.red;
                }
                else
                {
                    byte id = map.Get(x, y);
                    if (id >= set.Count) continue;
                    color = set.layers[id].previewColor;
                }

                color.a = gizmoAlpha;
                Gizmos.color = color;
                Gizmos.DrawCube(CellToWorld(x, y) + Vector3.up * 0.02f, size);
            }

        Gizmos.color = new Color(0f, 1f, 1f, 0.4f);
        Vector3 p0 = transform.TransformPoint(Vector3.zero);
        Vector3 p1 = transform.TransformPoint(new Vector3(width * cellSize, 0f, 0f));
        Vector3 p2 = transform.TransformPoint(new Vector3(width * cellSize, 0f, height * cellSize));
        Vector3 p3 = transform.TransformPoint(new Vector3(0f, 0f, height * cellSize));
        Gizmos.DrawLine(p0, p1); Gizmos.DrawLine(p1, p2);
        Gizmos.DrawLine(p2, p3); Gizmos.DrawLine(p3, p0);
    }
#endif
}
