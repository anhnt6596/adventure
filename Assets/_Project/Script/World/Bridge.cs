using System.Collections.Generic;
using UnityEngine;

// A bridge: art, plus a walkable footprint stamped over cells the terrain says are impassable. The terrain is not
// edited — the water underneath stays water, so the river still draws under the deck and the shore distance the
// water shader is built on never moves. See IDeck.
//
// THE RAILINGS ARE NOT AUTHORED. The collision boundary is generated per cell (TerrainGrid.CellFaces) and only a
// BLOCKED cell emits anything, on the edges that face walkable ground — so a decked cell falls silent while the
// water either side of it keeps emitting and fences the deck in by itself; and at the two ends, where deck meets
// land, neither side emits anything at all and you simply step on. Nothing here has to know which end is which.
//
// The footprint is a GridArea tested in this object's LOCAL space, which is why there is no orientation
// convention to remember: rotate the transform and the decked cells rotate with it, and a diagonal span comes out
// as the staircase of cells the wall bake already chamfers. The span is authored here rather than measured off the
// art — art overhangs, decorates and gets replaced, and walkability must not follow it.
//
// A diagonal deck wants to be at least 2 cells wide. A single-cell staircase pinches at every corner, and cell
// connectivity knows nothing about a body's radius: the path would be legal and the walk impossible.
[ExecuteAlways]
[DisallowMultipleComponent]
public class Bridge : MonoBehaviour, IDeck
{
    // A tile span by default: a deck ends up as a set of cells whatever shape drew it, so measuring it in cells is
    // the one authoring unit that cannot surprise you. Swap to BoxArea or LineArea for a diagonal or bending span.
    [SerializeReference] GridArea shape = new TileRectArea();
    [SerializeField] TerrainGrid grid;              // auto-found from the map if left empty
    [SerializeField] bool deck = true;

    public GridArea Shape => shape;

    // A drawbridge raises by turning this off. The art keeps animating on its own; walkability is a single flip,
    // and it costs one re-rasterise of the deck bitmap — no bake, in effect from the next query.
    public bool Deck
    {
        get => deck;
        set
        {
            if (deck == value) return;
            deck = value;
            ResolveGrid()?.MarkDecksDirty();
        }
    }

    public bool DeckActive => deck && isActiveAndEnabled && shape != null;

    TerrainGrid _joined;                            // the grid this deck is registered with
    Vector3 _lastPos;
    Quaternion _lastRot;
    Vector3 _lastScale;

    // The map's grid: an explicit ref, else the one on an ancestor, else anywhere under the map root — the same
    // resolution SpawnZone uses, so a bridge dropped into a map prefab needs no wiring.
    public TerrainGrid ResolveGrid()
    {
        if (grid != null) return grid;
        var found = GetComponentInParent<TerrainGrid>();
        if (found == null && transform.root != null) found = transform.root.GetComponentInChildren<TerrainGrid>(true);
        return found;
    }

    void OnEnable()
    {
        _joined = ResolveGrid();
        _joined?.AddDeck(this);
        CacheTransform();
    }

    void OnDisable()
    {
        _joined?.RemoveDeck(this);
        _joined = null;
    }

    void OnValidate()
    {
        // The shape or the grid reference was edited in the inspector.
        if (_joined != null && _joined != grid && grid != null)
        {
            _joined.RemoveDeck(this);
            _joined = grid;
            _joined.AddDeck(this);
        }
        ResolveGrid()?.MarkDecksDirty();
    }

    // Moving a bridge changes which cells it decks, and nothing tells us that happened — a transform has no
    // change callback. Four comparisons per bridge per frame is cheaper than any bookkeeping, and it covers
    // dragging one around in the editor and sliding one at runtime with the same code.
    void LateUpdate()
    {
        var t = transform;
        if (t.position == _lastPos && t.rotation == _lastRot && t.lossyScale == _lastScale) return;
        CacheTransform();
        ResolveGrid()?.MarkDecksDirty();
    }

    void CacheTransform()
    {
        var t = transform;
        _lastPos = t.position;
        _lastRot = t.rotation;
        _lastScale = t.lossyScale;
    }

    public void CollectDeckCells(TerrainGrid g, List<Vector2Int> into)
    {
        if (shape == null || g == null) return;
        shape.CollectCells(g, transform, into);   // a tile span answers exactly; a continuous shape rasterises
    }

#if UNITY_EDITOR
    // How many otherwise-separate stretches of walkable ground this deck joins, measured with every deck ignored.
    // 2 or more is a bridge. 1 is a jetty — it goes somewhere you could already reach. 0 means you cannot step
    // onto it at all, which is the failure that looks completely fine until you walk up to it.
    public int RegionsJoined()
    {
        var g = ResolveGrid();
        if (g == null || shape == null) return 0;

        var cells = new List<Vector2Int>();
        CollectDeckCells(g, cells);

        var touched = new HashSet<int>();
        foreach (var c in cells)
            foreach (var d in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
            {
                var n = c + d;
                if (g.HasDeck(n.x, n.y)) continue;              // still the bridge, not a shore
                int r = g.RegionOfWithoutDecks(n.x, n.y);
                if (r >= 0) touched.Add(r);
            }
        return touched.Count;
    }

    // The authored outline, always — it is one shape and it costs nothing.
    void OnDrawGizmos()
    {
        if (shape == null) return;
        var g = ResolveGrid();

        // A tile-measured span ignores rotation, so its outline must be drawn unrotated or it would show cells
        // that are not the ones it decks.
        Gizmos.matrix = shape.UsesRotation
            ? transform.localToWorldMatrix
            : Matrix4x4.Translate(transform.position);
        Gizmos.color = deck ? new Color(0.95f, 0.8f, 0.25f, 0.9f) : new Color(0.5f, 0.5f, 0.5f, 0.7f);
        shape.DrawGizmo(g != null ? g.CellSize : 1f);
        Gizmos.matrix = Matrix4x4.identity;
    }

    // The CELLS only while selected. The outline is what you authored, the cells are what the game gets, and on a
    // grid this coarse they are not the same picture — but working them out is a scan of the map per bridge, and
    // OnDrawGizmos runs for every bridge on every repaint. Same split SpawnZone uses for its baked cells.
    void OnDrawGizmosSelected()
    {
        var g = ResolveGrid();
        if (g == null || shape == null) return;

        var cells = new List<Vector2Int>();
        CollectDeckCells(g, cells);
        Gizmos.color = deck ? new Color(0.95f, 0.8f, 0.25f, 0.35f) : new Color(0.5f, 0.5f, 0.5f, 0.25f);
        var size = new Vector3(g.CellSize, 0.01f, g.CellSize) * 0.9f;
        foreach (var c in cells)
            Gizmos.DrawCube(g.CellToWorld(c.x, c.y) + Vector3.up * 0.06f, size);
    }
#endif
}
