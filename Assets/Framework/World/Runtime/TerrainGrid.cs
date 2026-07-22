using UnityEngine;

// Terrain is walkability, not buildability: a path around a house is walkable and unbuildable.
[ExecuteAlways]
public class TerrainGrid : MonoBehaviour
{
    [SerializeField] TerrainSet set;
    [SerializeField, Min(1)] int width = 64;
    [SerializeField, Min(1)] int height = 64;
    [SerializeField, Min(0.01f)] float cellSize = 1f;

    [SerializeField, HideInInspector] byte[] cells;
    [SerializeField, HideInInspector] WallSeg[] walls;   // baked walkable boundary

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
            // Keeps whatever still fits rather than dropping a painted map.
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

    public WallSeg[] Walls => walls;

    // Regenerates the walkable boundary from the current map. Baked (not computed per query) since it
    // depends on the whole map's layout; rebuild it when the paint changes.
    public void BakeWalkable()
        => walls = WalkBake.Bake(Map, set != null ? set.BuildWalkableTable() : null, cellSize);

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
    [SerializeField] bool drawWalkable = true;

    // The tile art has a mesh now, so no per-cell gizmo; just the field border and the baked walkable
    // boundary.
    void OnDrawGizmosSelected()
    {
        var tf = transform;

        Gizmos.color = new Color(0f, 1f, 1f, 0.35f);
        Vector3 p0 = tf.TransformPoint(Vector3.zero);
        Vector3 p1 = tf.TransformPoint(new Vector3(width * cellSize, 0f, 0f));
        Vector3 p2 = tf.TransformPoint(new Vector3(width * cellSize, 0f, height * cellSize));
        Vector3 p3 = tf.TransformPoint(new Vector3(0f, 0f, height * cellSize));
        Gizmos.DrawLine(p0, p1); Gizmos.DrawLine(p1, p2);
        Gizmos.DrawLine(p2, p3); Gizmos.DrawLine(p3, p0);

        if (!drawWalkable || walls == null) return;

        Gizmos.color = new Color(1f, 0.85f, 0.1f, 1f);
        foreach (var w in walls)
        {
            Vector3 a = tf.TransformPoint(new Vector3(w.a.x, 0.03f, w.a.y));
            Vector3 b = tf.TransformPoint(new Vector3(w.b.x, 0.03f, w.b.y));
            Gizmos.DrawLine(a, b);
        }
    }
#endif
}
