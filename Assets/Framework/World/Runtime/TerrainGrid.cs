using System.Collections.Generic;
using UnityEngine;

// Terrain is walkability, not buildability: a path around a house is walkable and unbuildable.
//
// THIS IS THE TILEMAP, AND IT KNOWS ABOUT NOTHING ELSE. Walkability here is the painted cells and only them:
//
//     standable(cell) = set.IsWalkable(cells[cell])
//
// Bridges, gates and anything else that overrides walkability are NOT here and never touch this. They are
// separate geometry combined with this one at the query layer - see TerrainQuery, which is what gameplay and
// collision actually ask. Nothing in this file has heard of them, so painting, face generation and regions
// behave exactly as they would on a map with no bridge on it.
//
// NOTHING HERE IS BAKED either: the collision boundary is generated one cell at a time, on demand, by CellFaces.
// There is no wall array to bake, to serialize, or to discover was stale after shipping - a tile being painted
// takes effect on the very next query. The depth-mask migration in Docs/TODO.md lands as one more term on the
// line above (&& height == 0).
//
// Generating faces per body beats reading a baked list, and by more the bigger the map gets: the old bake made
// one flat array of every boundary segment on the map and CollisionWorld scanned all of it for every body
// (~900 segments on a 64x64 map, ~14000 on a 256x256 one). A body can only touch the cells it overlaps, so nine
// cells is the real answer, whatever the map's size.
//
// REGIONS are the one thing still derived and cached: the connected components of the painted walkable set, which
// answer "can this even be reached" in O(1) - the most expensive question to ask a pathfinder. They are rebuilt
// when the paint changes and are never serialized. Bridges join regions together, but they do it in TerrainQuery,
// over the top of these; the numbers here are always the map without them.
[ExecuteAlways]
public class TerrainGrid : MonoBehaviour
{
    [SerializeField] TerrainSet set;
    [SerializeField, Min(1)] int width = 64;
    [SerializeField, Min(1)] int height = 64;
    [SerializeField, Min(0.01f)] float cellSize = 1f;

    [SerializeField, HideInInspector] byte[] cells;

    TerrainMap _map;
    bool[] _walkableById;

    int[] _region;                // per cell: connected component, -1 where nothing can stand
    int _regionCount;

    readonly Stack<int> _flood = new Stack<int>();
    bool _dirty = true;

    public TerrainSet Set => set;
    public int Width => width;
    public int Height => height;
    public float CellSize => cellSize;

    // Bumped whenever the painted walkable set changes, so a cached path can tell it went stale. Bridges have
    // their own version; TerrainQuery combines the two.
    public int WalkVersion { get; private set; }

    public TerrainMap Map
    {
        get
        {
            // An undo swaps in a fresh cells array, so identity has to be checked too - otherwise the map keeps
            // writing into the array Unity just discarded.
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
        _walkableById = set != null ? set.BuildWalkableTable() : null;
        _dirty = true;              // the cell grid moved under everything derived from it
    }

    // The paint changed, or the set did.
    public void MarkDirty()
    {
        _walkableById = set != null ? set.BuildWalkableTable() : null;
        _dirty = true;
    }

    // Refreshed lazily, on the query paths, rather than from an Update: a frame that paints several times
    // refreshes exactly once, at whatever reads the terrain first, and there is no update order to get right
    // against the collision tick.
    void EnsureFresh()
    {
        if (!_dirty) return;
        _dirty = false;

        RebuildRegions();
        WalkVersion++;
    }

    // ---- the walkable rule -------------------------------------------------

    // The single definition. Out of bounds is never standable.
    bool Standable(int x, int y)
    {
        if ((uint)x >= (uint)width || (uint)y >= (uint)height) return false;
        return _walkableById == null || _walkableById[cells[y * width + x]];
    }

    public bool IsWalkable(int x, int y)
    {
        EnsureFresh();
        return Standable(x, y);
    }

    // A body's pass mask over terrain ids, so a swimmer or a buff opens water without the terrain changing.
    public bool CanPass(int passMask, int x, int y)
    {
        EnsureFresh();
        if ((uint)x >= (uint)width || (uint)y >= (uint)height) return false;
        return (passMask & TerrainSet.BitOf(cells[y * width + x])) != 0;
    }

    public int DefaultPassMask => set != null ? set.BuildDefaultPassMask() : ~0;

    // ---- collision faces ---------------------------------------------------

    // The walkable boundary of ONE cell, written into `into` from index `at`, returning the new count. A cell that
    // can be stood on emits nothing.
    //
    // These are the TILEMAP's faces and nothing else's. A bridge does not silence them here - TerrainQuery drops
    // the parts of them its deck covers, on the way out.
    //
    // `into` needs WallSeg.MaxPerCell free slots. Same geometry the whole-map bake used to produce; see WallSeg.
    public int CellFaces(int x, int y, WallSeg[] into, int at)
    {
        EnsureFresh();
        if (!Blocked(x, y)) return at;

        const float D = WallSeg.Inset;
        const float V = WallSeg.Chamfer;

        byte id = cells[y * width + x];
        bool bN = Blocked(x, y + 1), bS = Blocked(x, y - 1), bE = Blocked(x + 1, y), bW = Blocked(x - 1, y);
        bool bNE = Blocked(x + 1, y + 1), bNW = Blocked(x - 1, y + 1);
        bool bSE = Blocked(x + 1, y - 1), bSW = Blocked(x - 1, y - 1);

        float x0 = x, x1 = x + 1, y0 = y, y1 = y + 1;

        // Straight inset edges, each end trimmed only at a convex corner: a straight run continues through, and a
        // concave corner is closed by the small bulge below instead.
        float tW = bW ? 0f : V, tE = bE ? 0f : V, tS = bS ? 0f : V, tN = bN ? 0f : V;

        if (!bN) at = Add(into, at, x0 + tW, y1 - D, x1 - tE, y1 - D, 0, 1, id);
        if (!bS) at = Add(into, at, x0 + tW, y0 + D, x1 - tE, y0 + D, 0, -1, id);
        if (!bE) at = Add(into, at, x1 - D, y0 + tS, x1 - D, y1 - tN, 1, 0, id);
        if (!bW) at = Add(into, at, x0 + D, y0 + tS, x0 + D, y1 - tN, -1, 0, id);

        // Convex corner (this cell alone sticks out): chamfer it off on the 45 degree line.
        if (!bN && !bE && !bNE) at = Add(into, at, x1 - V, y1 - D, x1 - D, y1 - V, 1, 1, id);
        if (!bN && !bW && !bNW) at = Add(into, at, x0 + V, y1 - D, x0 + D, y1 - V, -1, 1, id);
        if (!bS && !bE && !bSE) at = Add(into, at, x1 - V, y0 + D, x1 - D, y0 + V, 1, -1, id);
        if (!bS && !bW && !bSW) at = Add(into, at, x0 + V, y0 + D, x0 + D, y0 + V, -1, -1, id);

        // Concave corner (this cell wraps an open diagonal): a small bulge at the grid corner.
        if (bN && bE && !bNE) at = Add(into, at, x1, y1 - D, x1 - D, y1, 1, 1, id);
        if (bN && bW && !bNW) at = Add(into, at, x0, y1 - D, x0 + D, y1, -1, 1, id);
        if (bS && bE && !bSE) at = Add(into, at, x1, y0 + D, x1 - D, y0, 1, -1, id);
        if (bS && bW && !bSW) at = Add(into, at, x0, y0 + D, x0 + D, y0, -1, -1, id);

        return at;
    }

    // Out of bounds reads as NOT blocked, so a cell on the map's edge still fences its outer side. Movement past
    // the border is refused by IsWalkable, not by a face.
    bool Blocked(int x, int y)
        => (uint)x < (uint)width && (uint)y < (uint)height && !Standable(x, y);

    int Add(WallSeg[] into, int at, float ax, float ay, float bx, float by, float nx, float ny, byte id)
    {
        if (into == null || at >= into.Length) return at;
        into[at] = new WallSeg
        {
            a = new Vector2(ax * cellSize, ay * cellSize),
            b = new Vector2(bx * cellSize, by * cellSize),
            normal = new Vector2(nx, ny).normalized,
            terrain = id,
        };
        return at + 1;
    }

    // ---- regions -----------------------------------------------------------

    public int RegionCount { get { EnsureFresh(); return _regionCount; } }

    // -1 where nothing can stand. Two cells with the same id are connected by walking; different ids are not
    // reachable from each other at all, which a pathfinder can answer with before it expands a single node.
    public int RegionOf(int x, int y)
    {
        EnsureFresh();
        return (uint)x < (uint)width && (uint)y < (uint)height && _region != null ? _region[y * width + x] : -1;
    }

    void RebuildRegions()
    {
        int n = width * height;
        if (_region == null || _region.Length != n) _region = new int[n];

        _regionCount = Flood(_region);
    }

    // Four-neighbour on purpose: a convex corner is chamfered off, so a body physically cannot squeeze through a
    // diagonal gap. Counting diagonals as connected here would promise reachability the collision refuses.
    int Flood(int[] region)
    {
        for (int i = 0; i < region.Length; i++) region[i] = -1;

        int count = 0;
        for (int start = 0; start < region.Length; start++)
        {
            if (region[start] >= 0 || !Standable(start % width, start / width)) continue;

            region[start] = count;
            _flood.Push(start);
            while (_flood.Count > 0)
            {
                int i = _flood.Pop();
                int x = i % width, y = i / width;
                Step(region, x + 1, y, count);
                Step(region, x - 1, y, count);
                Step(region, x, y + 1, count);
                Step(region, x, y - 1, count);
            }
            count++;
        }
        return count;
    }

    void Step(int[] region, int x, int y, int id)
    {
        if ((uint)x >= (uint)width || (uint)y >= (uint)height) return;
        int i = y * width + x;
        if (region[i] >= 0 || !Standable(x, y)) return;
        region[i] = id;
        _flood.Push(i);
    }

    // ---- space -------------------------------------------------------------

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
    [SerializeField] bool drawWater = true;                      // water barely reads on the art map; overlay it
    [SerializeField, Range(0f, 1f)] float waterGizmoAlpha = 0.5f;

    // The tile art has a mesh now, so no per-cell gizmo; just the field border, the water overlay, and the
    // walkable boundary — generated for the whole map here, the same way collision generates it per body.
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

        // Only water cells — everything else keeps its art unobscured; uses the layer's previewColor.
        if (drawWater && set != null)
        {
            var map = Map;
            var cell = new Vector3(cellSize, 0.001f, cellSize);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int id = map.Get(x, y);
                    if (id >= set.Count || set.layers[id].kind != TerrainKind.Water) continue;
                    Color c = set.layers[id].previewColor;
                    c.a = waterGizmoAlpha;
                    Gizmos.color = c;
                    Gizmos.DrawCube(CellToWorld(x, y) + Vector3.up * 0.02f, cell);
                }
        }

        if (!drawWalkable) return;

        var faces = new WallSeg[WallSeg.MaxPerCell];
        Gizmos.color = new Color(1f, 0.85f, 0.1f, 1f);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int n = CellFaces(x, y, faces, 0);
                for (int i = 0; i < n; i++)
                {
                    Vector3 a = tf.TransformPoint(new Vector3(faces[i].a.x, 0.03f, faces[i].a.y));
                    Vector3 b = tf.TransformPoint(new Vector3(faces[i].b.x, 0.03f, faces[i].b.y));
                    Gizmos.DrawLine(a, b);
                }
            }
    }
#endif
}
