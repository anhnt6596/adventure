using UnityEngine;

// A bridge: art, plus a piece of walkable geometry it provides for itself. It is NOT painted into the tilemap and
// the tilemap has not heard of it — the water underneath stays water, so the river still draws under the deck and
// the shore distance the water shader is built on never moves. The two are combined only when a question is
// asked; see TerrainQuery.
//
// IT DOES NOT KNOW WHAT IT IS STANDING ON. There is no grid reference and no cell size here: the deck is a shape
// in this transform's own space, published in world space, and TerrainQuery converts it into whatever space it
// compares in. A bridge works the same on any map, or on none.
//
// THE DECK IS ITS OWN SHAPE, IN WORLD UNITS. It is not a set of cells and it is not measured against the grid, so
// a 1.4 unit deck is 1.4 units wide and a diagonal crossing is a rotated rectangle rather than a staircase. The
// span is authored here rather than measured off the art — art overhangs, decorates and gets replaced, and
// walkability must not follow it.
//
// THE RAILINGS COME FROM THE SHAPE. Its long sides are walls so you cannot walk off into the river; its two ends
// are open so you can step on. Where a side runs over walkable land — the last stretch at each shore —
// TerrainQuery drops it, so a bridge never fences off the bank it lands on. Nothing here has to know which end is
// which.
[ExecuteAlways]
[DisallowMultipleComponent]
// Before CollisionSystem (-100), so a deck that moved this frame is settled by the time the collision step reads
// it. The other way round the step ran first and everyone standing on a moving bridge lagged a frame behind it,
// which on a fast platform is a body sliding off the back of its own deck.
[DefaultExecutionOrder(-200)]
public class Bridge : MonoBehaviour, IWalkVolume
{
    [SerializeReference] BridgeShape shape = new DeckRect();

    [Tooltip("Off raises the drawbridge: the deck stops existing for every query. The art is not touched.")]
    [SerializeField] bool lowered = true;

    public BridgeShape Shape => shape;

    // A drawbridge raises by turning this off. The art keeps animating on its own; walkability is a single flip,
    // in effect from the next query — there is nothing to rebuild.
    public bool Lowered
    {
        get => lowered;
        set
        {
            if (lowered == value) return;
            lowered = value;
            _version++;
        }
    }

    // ---- IWalkVolume -------------------------------------------------------

    public bool VolumeActive => lowered && isActiveAndEnabled && shape != null;

    public int VolumeVersion => _version;

    public bool Contains(Vector3 world)
    {
        if (shape == null) return false;
        return shape.Contains(ToLocal(world));
    }

    public int MaxOverlapValues => shape != null ? shape.MaxOverlapValues : 0;

    // Both endpoints into this transform's space and let the shape solve there. t is a fraction along the
    // segment, and an affine change of frame does not move it, so the intervals come back usable as they are.
    public int Overlap(Vector3 a, Vector3 b, float[] into, int at)
        => shape == null ? at : shape.Overlap(ToLocal(a), ToLocal(b), into, at);

    public int MaxOutlineEdges => shape != null ? shape.MaxOutlineEdges : 0;

    public int Outline(WorldEdge[] into, int at)
    {
        if (shape == null || into == null) return at;

        int need = Mathf.Max(1, shape.MaxOutlineEdges);
        if (_outline.Length < need) _outline = new WallSeg[need];

        int n = shape.Outline(_outline, 0);
        for (int i = 0; i < n && at < into.Length; i++)
        {
            var e = _outline[i];
            into[at++] = new WorldEdge
            {
                a = _l2w.MultiplyPoint3x4(new Vector3(e.a.x, 0f, e.a.y)),
                b = _l2w.MultiplyPoint3x4(new Vector3(e.b.x, 0f, e.b.y)),
                inward = _l2w.MultiplyVector(new Vector3(e.normal.x, 0f, e.normal.y)),
            };
        }
        return at;
    }

    public Bounds WorldBounds
    {
        get
        {
            var r = shape != null ? shape.LocalBounds : new Rect();

            Vector3 c0 = _l2w.MultiplyPoint3x4(new Vector3(r.xMin, 0f, r.yMin));
            Vector3 c1 = _l2w.MultiplyPoint3x4(new Vector3(r.xMax, 0f, r.yMin));
            Vector3 c2 = _l2w.MultiplyPoint3x4(new Vector3(r.xMax, 0f, r.yMax));
            Vector3 c3 = _l2w.MultiplyPoint3x4(new Vector3(r.xMin, 0f, r.yMax));

            var b = new Bounds(c0, Vector3.zero);
            b.Encapsulate(c1);
            b.Encapsulate(c2);
            b.Encapsulate(c3);
            return b;
        }
    }

    // The cached inverse, not a live InverseTransformPoint. Contains is asked once per cell while the reachability
    // index rebuilds and once per point while bodies move, and Overlap converts a segment per query - a native
    // transform call at each was the largest single cost on the composed path.
    Vector2 ToLocal(Vector3 world)
    {
        Vector3 p = _w2l.MultiplyPoint3x4(world);
        return new Vector2(p.x, p.z);
    }

    // ---- change tracking ---------------------------------------------------

    int _version = 1;
    WallSeg[] _outline = new WallSeg[4];    // the shape's outline in local space, before it goes out as world edges

    // Refreshed in the same breath as the version bump, so what Contains answers and the outline TerrainQuery
    // cached off the back of that bump always describe the same bridge in the same place. A live transform read
    // here would be the one part of the deck that could disagree with the rest of it.
    Matrix4x4 _l2w = Matrix4x4.identity;
    Matrix4x4 _w2l = Matrix4x4.identity;

    Vector3 _lastPos;
    Quaternion _lastRot;
    Vector3 _lastScale;

    void Awake() => CacheTransform();

    void OnEnable()
    {
        CacheTransform();
        _version++;
        CollisionSystem.Instance?.Register(this);
    }

    void OnDisable()
    {
        CollisionSystem.Instance?.Unregister(this);
        _version++;
    }

    // Also re-snaps the matrices: OnValidate can be the first thing that runs on a freshly added component, and
    // the editor's validation asks a bridge where it is without waiting for it to be enabled.
    void OnValidate()
    {
        CacheTransform();
        _version++;
    }

    // Moving a bridge moves its geometry, and nothing tells us that happened — a transform has no change
    // callback. Three comparisons per bridge per frame is cheaper than any bookkeeping, and it covers dragging
    // one around in the editor and sliding one at runtime with the same code.
    void LateUpdate()
    {
        var t = transform;
        if (t.position == _lastPos && t.rotation == _lastRot && t.lossyScale == _lastScale) return;
        CacheTransform();
        _version++;
    }

    void CacheTransform()
    {
        var t = transform;
        _lastPos = t.position;
        _lastRot = t.rotation;
        _lastScale = t.lossyScale;
        _l2w = t.localToWorldMatrix;
        _w2l = t.worldToLocalMatrix;
    }

#if UNITY_EDITOR
    // Editor-only, and the ONE place a bridge asks about the map it is on: validation has to name the terrain it
    // is validating against. Resolved on the spot rather than serialized, so the component itself stays free of it.
    public TerrainGrid ResolveGrid()
    {
        var found = GetComponentInParent<TerrainGrid>();
        if (found == null && transform.root != null) found = transform.root.GetComponentInChildren<TerrainGrid>(true);
        return found;
    }

    // How many otherwise-separate stretches of walkable ground this deck joins, measured against the tilemap's own
    // regions — which are always the map with no bridge in it. 2 or more is a bridge. 1 is a jetty: it goes
    // somewhere you could already reach. 0 means you cannot step onto it at all, which is the failure that looks
    // completely fine until you walk up to it.
    public int RegionsJoined()
    {
        var g = ResolveGrid();
        if (g == null || shape == null) return 0;
        return new TerrainQuery(g).RegionsJoined(this);
    }

    // The deck outline in full: solid where its sides are walls, dashed at the open ends. This is the geometry
    // the game gets — there is no second, coarser picture of it any more.
    void OnDrawGizmos()
    {
        if (shape == null) return;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = lowered ? new Color(0.95f, 0.8f, 0.25f, 0.9f) : new Color(0.5f, 0.5f, 0.5f, 0.7f);
        shape.DrawGizmo();
        Gizmos.matrix = Matrix4x4.identity;
    }
#endif
}
