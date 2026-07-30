using System.Collections.Generic;
using UnityEngine;

// Terrain is walkability, not buildability: a path around a house is walkable and unbuildable.
//
// NOTHING HERE IS BAKED. Walkability is answered by reading two maps live - the painted cells and the deck bitmap
// that bridges stamp over them (see IDeck):
//
//     standable(cell) = deck[cell] || set.IsWalkable(cells[cell])
//
// and the collision boundary is generated one cell at a time, on demand, by CellFaces. There is no wall array to
// bake, to serialize, to keep in step with the decks, or to discover was stale after shipping. A deck raising or
// a tile being painted takes effect on the very next query. The walkable rule also has exactly one home, so the
// collision, the queries and (next) the pathfinder cannot disagree about where a bridge is - not because they are
// carefully synchronised, but because there is no second copy to drift from. The depth-mask migration in
// Docs/TODO.md lands as one more term on that line (&& height == 0).
//
// Generating faces per body beats reading a baked list, and by more the bigger the map gets: the old bake made
// one flat array of every boundary segment on the map and CollisionWorld scanned all of it for every body
// (~900 segments on a 64x64 map, ~14000 on a 256x256 one). A body can only touch the cells it overlaps, so nine
// cells is the real answer, whatever the map's size.
//
// REGIONS are the one thing still derived and cached: the connected components of the walkable set, which answer
// "can this even be reached" in O(1) - the most expensive question to ask a pathfinder, and one a raised
// drawbridge makes common. They are rebuilt when the walkable set changes and are never serialized. A second
// region map is built with every deck ignored, purely so a bridge can be told whether it joins anything.
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

    bool[] _deck;                 // per cell: a deck overrides the terrain here
    int[] _region, _regionNoDeck; // per cell: connected component, -1 where nothing can stand
    int _regionCount, _regionCountNoDeck;

    readonly List<IDeck> _decks = new List<IDeck>();
    readonly List<Vector2Int> _deckBuffer = new List<Vector2Int>();
    readonly Stack<int> _flood = new Stack<int>();
    bool _dirty = true;

    public TerrainSet Set => set;
    public int Width => width;
    public int Height => height;
    public float CellSize => cellSize;

    // Bumped whenever the walkable set changes. A pathfinder stamps its cached path with this and re-paths when it
    // no longer matches - which is what keeps a drawbridge closing from leaving agents walking into ground that
    // stopped existing under them.
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

    // ---- decks -------------------------------------------------------------

    public void AddDeck(IDeck deck)
    {
        if (deck == null || _decks.Contains(deck)) return;
        _decks.Add(deck);
        _dirty = true;
    }

    public void RemoveDeck(IDeck deck)
    {
        if (_decks.Remove(deck)) _dirty = true;
    }

    // A deck moved, resized, or raised itself.
    public void MarkDecksDirty() => _dirty = true;

    public int DeckCount => _decks.Count;

    // Refreshed lazily, on the query paths, rather than from an Update: a frame that toggles three decks refreshes
    // exactly once, at whatever reads the terrain first, and there is no update order to get right against the
    // collision tick.
    void EnsureFresh()
    {
        if (!_dirty) return;
        _dirty = false;

        var map = Map;
        int n = width * height;

        if (_deck == null || _deck.Length != n) _deck = new bool[n];
        else System.Array.Clear(_deck, 0, n);

        // The deck bitmap is the one cache left, and it is rasterised from the bridges rather than tested live:
        // asking every bridge's shape per cell query would be O(decks) on the collision path, and the shapes live
        // on the game side while this does not. It is refreshed from an explicit signal, never guessed at.
        for (int i = 0; i < _decks.Count; i++)
        {
            var d = _decks[i];
            if (d == null || !d.DeckActive) continue;

            _deckBuffer.Clear();
            d.CollectDeckCells(this, _deckBuffer);
            for (int k = 0; k < _deckBuffer.Count; k++)
            {
                var c = _deckBuffer[k];
                if (map.InBounds(c.x, c.y)) _deck[c.y * width + c.x] = true;
            }
        }

        RebuildRegions();
        WalkVersion++;
    }

    // ---- the walkable rule -------------------------------------------------

    // The single definition, and the only place the two maps meet. Out of bounds is never standable.
    bool Standable(int x, int y)
    {
        if ((uint)x >= (uint)width || (uint)y >= (uint)height) return false;
        int i = y * width + x;
        return (_deck != null && _deck[i]) || _walkableById == null || _walkableById[cells[i]];
    }

    public bool IsWalkable(int x, int y)
    {
        EnsureFresh();
        return Standable(x, y);
    }

    // A body's pass mask over terrain ids, so a swimmer or a buff opens water without the terrain changing. A deck
    // is passable to everyone: the plank is there whatever you are.
    public bool CanPass(int passMask, int x, int y)
    {
        EnsureFresh();
        if ((uint)x >= (uint)width || (uint)y >= (uint)height) return false;
        int i = y * width + x;
        if (_deck != null && _deck[i]) return true;
        return (passMask & TerrainSet.BitOf(cells[i])) != 0;
    }

    public bool HasDeck(int x, int y)
    {
        EnsureFresh();
        return (uint)x < (uint)width && (uint)y < (uint)height && _deck != null && _deck[y * width + x];
    }

    public int DefaultPassMask => set != null ? set.BuildDefaultPassMask() : ~0;

    // ---- collision faces ---------------------------------------------------

    // The walkable boundary of ONE cell, written into `into` from index `at`, returning the new count. A cell that
    // can be stood on emits nothing - which is exactly why a bridge deck needs no code of its own: the deck cells
    // fall silent and the water either side of them keeps emitting, fencing the deck in, while at the two ends
    // deck meets land and neither side emits anything at all.
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
    public int RegionCountWithoutDecks { get { EnsureFresh(); return _regionCountNoDeck; } }

    // -1 where nothing can stand. Two cells with the same id are connected by walking; different ids are not
    // reachable from each other at all, which a pathfinder can answer with before it expands a single node.
    public int RegionOf(int x, int y)
    {
        EnsureFresh();
        return (uint)x < (uint)width && (uint)y < (uint)height && _region != null ? _region[y * width + x] : -1;
    }

    public bool SameRegion(int ax, int ay, int bx, int by)
    {
        int a = RegionOf(ax, ay);
        return a >= 0 && a == RegionOf(bx, by);
    }

    // The regions the map would have with every deck ignored. Only a deck's own validation wants this: it is how a
    // bridge is told whether it joins two shores or merely sticks out of one.
    public int RegionOfWithoutDecks(int x, int y)
    {
        EnsureFresh();
        return (uint)x < (uint)width && (uint)y < (uint)height && _regionNoDeck != null
            ? _regionNoDeck[y * width + x] : -1;
    }

    void RebuildRegions()
    {
        int n = width * height;
        if (_region == null || _region.Length != n) _region = new int[n];
        if (_regionNoDeck == null || _regionNoDeck.Length != n) _regionNoDeck = new int[n];

        _regionCount = Flood(_region, false);
        // The same flood over the terrain alone. Not "the walkable set minus the deck cells": a deck laid over
        // ground that was already walkable is not a barrier, and treating it as one would split a region that
        // never split.
        _regionCountNoDeck = Flood(_regionNoDeck, true);
    }

    // Four-neighbour on purpose: a convex corner is chamfered off, so a body physically cannot squeeze through a
    // diagonal gap. Counting diagonals as connected here would promise reachability the collision refuses.
    int Flood(int[] region, bool ignoreDecks)
    {
        for (int i = 0; i < region.Length; i++) region[i] = -1;

        int count = 0;
        for (int start = 0; start < region.Length; start++)
        {
            if (region[start] >= 0 || !Open(start % width, start / width, ignoreDecks)) continue;

            region[start] = count;
            _flood.Push(start);
            while (_flood.Count > 0)
            {
                int i = _flood.Pop();
                int x = i % width, y = i / width;
                Step(region, x + 1, y, count, ignoreDecks);
                Step(region, x - 1, y, count, ignoreDecks);
                Step(region, x, y + 1, count, ignoreDecks);
                Step(region, x, y - 1, count, ignoreDecks);
            }
            count++;
        }
        return count;
    }

    bool Open(int x, int y, bool ignoreDecks)
    {
        if ((uint)x >= (uint)width || (uint)y >= (uint)height) return false;
        if (ignoreDecks) return _walkableById == null || _walkableById[cells[y * width + x]];
        return Standable(x, y);
    }

    void Step(int[] region, int x, int y, int id, bool ignoreDecks)
    {
        if ((uint)x >= (uint)width || (uint)y >= (uint)height) return;
        int i = y * width + x;
        if (region[i] >= 0 || !Open(x, y, ignoreDecks)) return;
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
