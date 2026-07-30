using System.Collections.Generic;
using UnityEngine;

// THE ONLY PLACE THE TWO SOURCES MEET, and it meets them at the moment a question is asked.
//
// There are two independent geometries and neither is ever written into the other:
//
//   * the TILEMAP (TerrainGrid) - painted cells, its own faces, its own regions. It has not heard of bridges and
//     behaves exactly as it would on a map with none. Nothing here writes to it.
//   * the WALK VOLUMES (IWalkVolume) - free-form deck geometry each bridge provides for itself, analytic and
//     sub-cell. Nothing rasterises them into cells.
//
// Every answer is composed on the way out:
//
//     walkable(p)   = anyVolumeCovers(p) || tilemap.walkable(cell(p))
//     faces(cell)   = tilemap.faces(cell) minus the parts a volume covers,
//                     plus each volume's own rails minus the parts over walkable land
//
// The second line is the whole trick, and it is symmetric: a deck cuts the tilemap's wall away where it crosses
// the river, and the land cuts the deck's rails away where it meets the shore. Neither edit is stored - both are
// recomputed per query, so a bridge sliding, raising or being deleted takes effect on the very next question and
// there is no bake, no merged polygon and nothing that can go stale.
//
// The one derived thing is the REACHABILITY index (regions unioned by the bridges that join them), because
// "can this be reached at all" has to be O(1) and the tilemap's own regions cannot know about a bridge. It is
// rebuilt from a version stamp, never guessed at, and it is an index over the tilemap's regions - not a new set.
public class TerrainQuery
{
    // A cell can hand back the tilemap's faces (up to WallSeg.MaxPerCell), each possibly split by a deck edge,
    // plus rail pieces. Generous on purpose: silently dropping a face is a hole you can walk through.
    public const int MaxFacesPerCell = 48;

    TerrainGrid _terrain;
    readonly List<IWalkVolume> _volumes = new List<IWalkVolume>();

    readonly WallSeg[] _tileScratch = new WallSeg[WallSeg.MaxPerCell];
    readonly WallSeg[] _faceScratch = new WallSeg[WallSeg.MaxPerCell];   // InsetSkin, while _tileScratch is in use
    WorldEdge[] _worldEdges = new WorldEdge[16];

    // A volume speaks world space, because a bridge has no business knowing what a cell is. The conversion into
    // the space the tilemap's faces live in happens HERE, once per volume per move, and is cached - the collision
    // path visits nine cells per body per tick and must not pay for it each time.
    struct Cached
    {
        public bool valid;
        public int version;
        public Rect bounds;        // terrain-local XZ
        public WallSeg[] outline;  // terrain-local
        public int outlineCount;
    }

    Cached[] _cache = new Cached[4];
    Matrix4x4 _cacheSpace;         // the terrain transform the cache was built against
    Matrix4x4 _cacheSpaceInv;      // and its inverse, so converting a point costs a multiply and not a native call

    // The union of every active volume's bounds. Almost every cell on the map has no bridge anywhere near it, and
    // this answers that for all of them with ONE Rect test instead of one per volume. CellFaces runs some nine
    // cells per body per iteration - the per-volume scan was a cost the whole map paid for geometry covering a few
    // dozen cells of it.
    Rect _unionBounds;
    bool _anyVolume;

    // The terrain's own transform is asked for at most once per frame. Everything else EnsureCache does on a quiet
    // frame is an int compare per volume, but localToWorldMatrix is a native call and this sits in the collision
    // hot path. A map that MOVES is one being dragged in the editor or slid in a cutscene, where a frame of lag is
    // invisible; a volume moving or a drawbridge flipping is still picked up on the very next query, because that
    // is a version compare and it is never skipped.
    int _spaceFrame = -1;

    public TerrainQuery(TerrainGrid terrain) => _terrain = terrain;

    public TerrainGrid Terrain => _terrain;
    public int VolumeCount => _volumes.Count;

    public void SetTerrain(TerrainGrid terrain)
    {
        _terrain = terrain;
        Invalidate();
    }

    public void Add(IWalkVolume volume)
    {
        if (volume == null || _volumes.Contains(volume)) return;
        _volumes.Add(volume);
        Invalidate();
    }

    public void Remove(IWalkVolume volume)
    {
        if (_volumes.Remove(volume)) Invalidate();
    }

    void Invalidate()
    {
        _indexVersion = -1;
        _spaceFrame = -1;
        _anyVolume = false;
        for (int i = 0; i < _cache.Length; i++) _cache[i].valid = false;
    }

    // Refreshes any volume whose geometry moved, and everything if the map itself did. On a quiet frame this is
    // one int compare per volume and nothing else.
    void EnsureCache()
    {
        if (_terrain == null) return;

        bool spaceMoved = false;
#if UNITY_EDITOR
        bool timed = Application.isPlaying;   // edit mode has no dependable frame clock, so always look
#else
        const bool timed = true;
#endif
        if (!timed || Time.frameCount != _spaceFrame)
        {
            _spaceFrame = Time.frameCount;
            var space = _terrain.transform.localToWorldMatrix;
            if (space != _cacheSpace)
            {
                _cacheSpace = space;
                _cacheSpaceInv = space.inverse;
                spaceMoved = true;
            }
        }

        if (_cache.Length < _volumes.Count)
        {
            var grown = new Cached[Mathf.Max(_volumes.Count, _cache.Length * 2)];
            System.Array.Copy(_cache, grown, _cache.Length);
            _cache = grown;
        }

        _anyVolume = false;

        for (int i = 0; i < _volumes.Count; i++)
        {
            var v = _volumes[i];
            if (v == null || !v.VolumeActive) { _cache[i].valid = false; continue; }

            if (!_cache[i].valid || spaceMoved || _cache[i].version != v.VolumeVersion)
                Rebuild(i, v);

            // The broadphase union, accumulated here so it can never disagree with what it summarises.
            if (!_anyVolume) { _unionBounds = _cache[i].bounds; _anyVolume = true; }
            else
            {
                var b = _cache[i].bounds;
                _unionBounds = Rect.MinMaxRect(Mathf.Min(_unionBounds.xMin, b.xMin), Mathf.Min(_unionBounds.yMin, b.yMin),
                                               Mathf.Max(_unionBounds.xMax, b.xMax), Mathf.Max(_unionBounds.yMax, b.yMax));
            }
        }
    }

    void Rebuild(int i, IWalkVolume v)
    {
        // Bounds: the world AABB's four XZ corners brought into terrain space, then boxed again. Conservative
        // when the map is rotated, which is all a broadphase needs.
        var wb = v.WorldBounds;
        Vector2 b0 = ToLocal(new Vector3(wb.min.x, 0f, wb.min.z));
        Vector2 b1 = ToLocal(new Vector3(wb.max.x, 0f, wb.min.z));
        Vector2 b2 = ToLocal(new Vector3(wb.max.x, 0f, wb.max.z));
        Vector2 b3 = ToLocal(new Vector3(wb.min.x, 0f, wb.max.z));

        float x0 = Mathf.Min(Mathf.Min(b0.x, b1.x), Mathf.Min(b2.x, b3.x));
        float x1 = Mathf.Max(Mathf.Max(b0.x, b1.x), Mathf.Max(b2.x, b3.x));
        float y0 = Mathf.Min(Mathf.Min(b0.y, b1.y), Mathf.Min(b2.y, b3.y));
        float y1 = Mathf.Max(Mathf.Max(b0.y, b1.y), Mathf.Max(b2.y, b3.y));
        _cache[i].bounds = new Rect(x0, y0, x1 - x0, y1 - y0);

        int need = Mathf.Max(1, v.MaxOutlineEdges);
        if (_worldEdges.Length < need) _worldEdges = new WorldEdge[need];
        if (_cache[i].outline == null || _cache[i].outline.Length < need) _cache[i].outline = new WallSeg[need];

        int n = v.Outline(_worldEdges, 0);
        for (int e = 0; e < n; e++)
        {
            var we = _worldEdges[e];
            Vector3 inward = _cacheSpaceInv.MultiplyVector(we.inward);
            _cache[i].outline[e] = new WallSeg
            {
                a = ToLocal(we.a),
                b = ToLocal(we.b),
                normal = new Vector2(inward.x, inward.z).normalized,
                terrain = WallSeg.VolumeRail,
            };
        }
        _cache[i].outlineCount = n;
        _cache[i].version = v.VolumeVersion;
        _cache[i].valid = true;
    }

    // The cached matrices, not Transform.InverseTransformPoint: a segment is converted per volume per query and
    // the native call was the largest single cost in the composed path. Valid from the first EnsureCache onward,
    // which every public entry point runs before it converts anything.
    Vector2 ToLocal(Vector3 world)
    {
        Vector3 p = _cacheSpaceInv.MultiplyPoint3x4(world);
        return new Vector2(p.x, p.z);
    }

    Vector3 ToWorld(Vector2 local)
        => _cacheSpace.MultiplyPoint3x4(new Vector3(local.x, 0f, local.y));

    // Changes whenever anything a query depends on moves: the paint, or any volume. A cached path stamps itself
    // with this and re-paths when it no longer matches.
    public int WalkVersion
    {
        get
        {
            int v = _terrain != null ? _terrain.WalkVersion : 0;
            for (int i = 0; i < _volumes.Count; i++)
                if (_volumes[i] != null) v = v * 31 + _volumes[i].VolumeVersion;
            return v;
        }
    }

    // ---- coverage ----------------------------------------------------------

    // Is this world point on any deck? The volumes answer analytically, so this is true at sub-cell precision.
    public bool Covered(Vector3 world)
    {
        if (_terrain == null) return false;
        EnsureCache();

        if (!_anyVolume) return false;

        Vector2 local = ToLocal(world);
        if (!_unionBounds.Contains(local)) return false;

        for (int i = 0; i < _volumes.Count; i++)
        {
            if (!_cache[i].valid || !_cache[i].bounds.Contains(local)) continue;
            if (_volumes[i].Contains(world)) return true;
        }
        return false;
    }

    // ---- point queries -----------------------------------------------------

    // On a deck you can always stand; otherwise the tilemap decides. Off-map with no deck is never walkable.
    public bool IsWalkable(Vector3 world)
    {
        if (_terrain == null) return true;
        if (Covered(world)) return true;
        return _terrain.WorldToCell(world, out int x, out int y) && _terrain.IsWalkable(x, y);
    }

    // The same, through a body's pass mask. A deck is passable to everyone: the plank is there whatever you are.
    public bool CanPass(int passMask, Vector3 world)
    {
        if (_terrain == null) return true;
        if (passMask < 0) passMask = _terrain.DefaultPassMask;
        if (Covered(world)) return true;
        _terrain.WorldToCell(world, out int x, out int y);
        return _terrain.CanPass(passMask, x, y);
    }

    // ---- faces -------------------------------------------------------------

    // The walls of one cell, composed. Same contract as TerrainGrid.CellFaces (append from `at`, return the new
    // count) so the collision loop is unchanged apart from what it asks.
    public int CellFaces(int cx, int cy, WallSeg[] into, int at)
    {
        if (_terrain == null) return at;
        EnsureCache();

        float cs = _terrain.CellSize;
        var cell = new Rect(cx * cs, cy * cs, cs, cs);

        // Almost every cell on the map has no bridge anywhere near it, and this is the collision hot path. The
        // union answers that for the whole map in one test; only a cell inside it pays the per-volume scan.
        bool near = false;
        if (_anyVolume && _unionBounds.Overlaps(cell))
            for (int i = 0; i < _volumes.Count && !near; i++)
                near = _cache[i].valid && _cache[i].bounds.Overlaps(cell);

        // The tilemap's own faces, with the parts a deck covers cut out. The tilemap is not consulted about this
        // and does not change - the cut happens here, on a copy, per query.
        int n = _terrain.CellFaces(cx, cy, _tileScratch, 0);
        if (!near)
        {
            for (int i = 0; i < n; i++) at = Emit(_tileScratch[i], _tileScratch[i].a, _tileScratch[i].b, into, at);
            return at;
        }

        // Half one: the tilemap's boundary minus the interior of every deck.
        for (int i = 0; i < n; i++)
            at = ClipAndEmit(_tileScratch[i], -1, into, at);

        // Half two, the mirror image: each deck's whole outline, cut back to this cell, minus everything ELSE
        // that is already walkable - the land it lands on, and any other deck it meets. What is left is a wall.
        // Nothing here knows which edge was an "end"; an end simply has nothing left of it.
        for (int v = 0; v < _volumes.Count; v++)
        {
            if (!_cache[v].valid || !_cache[v].bounds.Overlaps(cell)) continue;

            for (int i = 0; i < _cache[v].outlineCount; i++)
                if (ClipToRect(_cache[v].outline[i], cell, out WallSeg piece))
                    at = ClipAndEmit(piece, v, into, at);
        }
        WarnIfFull(at, into, cx, cy);
        return at;
    }

    // Emit drops a face when the buffer is full, and a dropped face is a hole in a wall you can walk through -
    // silent everywhere except at the one spot someone eventually falls into the river. Only a cell with several
    // decks crossing it can get near MaxFacesPerCell, so this stays quiet on every real map; if it ever speaks,
    // raise the constant. Once per session, because it would otherwise fire every tick of every body.
    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    void WarnIfFull(int at, WallSeg[] into, int cx, int cy)
    {
        if (_warnedFull || into == null || at < into.Length) return;
        _warnedFull = true;
        Debug.LogWarning($"TerrainQuery: cell ({cx},{cy}) filled all {into.Length} face slots, so faces may have " +
                         $"been dropped - raise TerrainQuery.MaxFacesPerCell.");
    }

    bool _warnedFull;

    float[] _ranges = new float[64];

    // Emits the parts of `seg` that are NOT already walkable by some other source, keeping the original normal
    // and terrain id. `owner` is the index of the volume the segment came from, or -1 when it came from the
    // tilemap; that source is excluded, since a boundary is never cancelled by the thing it bounds.
    //
    // The stretches to remove are computed EXACTLY - a box or disc intersection for a deck, a cell walk for the
    // tilemap - never sampled. Sampling would silently keep a wall wherever the thing cutting it is narrower
    // than the sample spacing, which is an invisible wall across a bridge that no one can find later.
    int ClipAndEmit(WallSeg seg, int owner, WallSeg[] into, int at)
    {
        int n = WalkableRanges(seg.a, seg.b, owner);
        if (n == 0) return Emit(seg, seg.a, seg.b, into, at);

        // The complement of the removed stretches: the gaps between them, plus the ends.
        float cursor = 0f;
        for (int i = 0; i < n; i += 2)
        {
            if (_ranges[i] > cursor)
                at = Emit(seg, Vector2.Lerp(seg.a, seg.b, cursor), Vector2.Lerp(seg.a, seg.b, _ranges[i]), into, at);
            if (_ranges[i + 1] > cursor) cursor = _ranges[i + 1];
        }
        if (cursor < 1f)
            at = Emit(seg, Vector2.Lerp(seg.a, seg.b, cursor), seg.b, into, at);
        return at;
    }

    // Copies rather than just reallocating: the only caller that grows mid-fill is TerrainRanges sizing for both
    // halves at once, but a shape that grew the buffer from inside Overlap would otherwise drop what was already
    // there and lose a stretch of wall with nothing to show for it.
    void EnsureRangeRoom(int need)
    {
        if (_ranges.Length >= need) return;
        var grown = new float[Mathf.NextPowerOfTwo(need)];
        System.Array.Copy(_ranges, grown, _ranges.Length);
        _ranges = grown;
    }

    // The stretches of the segment that some source OTHER than `owner` already makes walkable, merged into
    // disjoint intervals in order. This is the "\ interior(everything else)" of the boundary rule, and it is the
    // only subtraction in the system - both halves of the wall go through it.
    //
    // owner == -1 means the segment is the tilemap's, so the tilemap is skipped and only decks can cut it.
    // owner >= i means it is deck i's, so the tilemap and every OTHER deck can cut it, and it cannot cut itself.
    int WalkableRanges(Vector2 a, Vector2 b, int owner)
    {
        int need = 8;
        for (int i = 0; i < _volumes.Count; i++)
            if (i != owner && _volumes[i] != null) need += _volumes[i].MaxOverlapValues;

        int at = 0;
        if (owner >= 0)
        {
            at = TerrainRanges(a, b, need);        // sizes the buffer for both halves
        }
        else EnsureRangeRoom(need);

        if (_volumes.Count > 0)
        {
            Vector3 wa = ToWorld(a), wb = ToWorld(b);
            for (int i = 0; i < _volumes.Count; i++)
                if (i != owner && _cache[i].valid) at = _volumes[i].Overlap(wa, wb, _ranges, at);
        }
        return Merge(_ranges, at);
    }

    // The stretches of the segment standing on walkable tilemap.
    //
    // EVERY GEOMETRIC DECISION IS MADE AGAINST THE WALL, NOT THE CELLS. Walking the cells is only how the nearby
    // faces are found - a spatial lookup - and where a piece starts and stops is settled by the faces themselves
    // (InsetSkin). Ask the cells instead and a deck's outline stops on the cell line, WallSeg.Inset short of the
    // wall, leaving a stub of railing hanging past the shore. That was a real artefact, visible in the editor.
    //
    // The cell grid is consulted for exactly ONE BIT per cell - open ground, or solid - and that bit is the one
    // thing the wall provably cannot supply: open ground emits no faces, and so does a cell deep inside a rock.
    // Same wall data, opposite answers. A boundary does not define a region without a seed, and recovering the
    // seed by counting ray crossings would need a seed of its own. So: wall for all geometry, tilemap for
    // inside-or-outside, O(1), and nothing else crosses over.
    //
    // `extra` is room the caller still needs after this.
    int TerrainRanges(Vector2 a, Vector2 b, int extra)
    {
        float cs = _terrain.CellSize;
        Vector2 ca = a / cs, cb = b / cs;

        int x = Mathf.FloorToInt(ca.x), y = Mathf.FloorToInt(ca.y);
        int ex = Mathf.FloorToInt(cb.x), ey = Mathf.FloorToInt(cb.y);
        int steps = Mathf.Abs(ex - x) + Mathf.Abs(ey - y) + 1;

        EnsureRangeRoom(2 * steps * WallSeg.MaxPerCell + 4 + extra);

        float dx = cb.x - ca.x, dy = cb.y - ca.y;
        int stepX = dx > 0f ? 1 : (dx < 0f ? -1 : 0);
        int stepY = dy > 0f ? 1 : (dy < 0f ? -1 : 0);

        float tDeltaX = stepX != 0 ? Mathf.Abs(1f / dx) : float.PositiveInfinity;
        float tDeltaY = stepY != 0 ? Mathf.Abs(1f / dy) : float.PositiveInfinity;
        float tMaxX = stepX > 0 ? (x + 1 - ca.x) * tDeltaX : (stepX < 0 ? (ca.x - x) * tDeltaX : float.PositiveInfinity);
        float tMaxY = stepY > 0 ? (y + 1 - ca.y) * tDeltaY : (stepY < 0 ? (ca.y - y) * tDeltaY : float.PositiveInfinity);

        int at = 0;
        float enter = 0f;

        for (int s = 0; s < steps; s++)
        {
            bool last = x == ex && y == ey;
            float exit = last ? 1f : Mathf.Min(1f, Mathf.Min(tMaxX, tMaxY));

            if (exit > enter)
                at = _terrain.IsWalkable(x, y) ? Push(at, enter, exit)
                                               : InsetSkin(a, b, x, y, enter, exit, at);

            if (last) break;
            enter = exit;
            if (tMaxX < tMaxY) { x += stepX; tMaxX += tDeltaX; }
            else { y += stepY; tMaxY += tDeltaY; }
        }
        return Merge(_ranges, at);
    }

    int Push(int at, float t0, float t1)
    {
        if (at + 1 >= _ranges.Length || t1 <= t0) return at;
        _ranges[at] = t0;
        _ranges[at + 1] = t1;
        return at + 2;
    }

    // A BLOCKED CELL IS NOT BLOCKED ALL THE WAY TO ITS EDGE. Its faces are inset (WallSeg.Inset) and chamfered,
    // so between the cell line and the cell's own wall there is a skin of ground you can stand on - that inset is
    // what makes walking along a shore feel right, and it has been there since long before bridges.
    //
    // A deck's outline has to stop at that WALL, not at the cell line, or it leaves a stub of railing poking out
    // past the shore into ground you can walk on. So "already walkable" inside a blocked cell means: on the open
    // side of at least one of the cell's own faces.
    int InsetSkin(Vector2 a, Vector2 b, int cx, int cy, float enter, float exit, int at)
    {
        int n = _terrain.CellFaces(cx, cy, _faceScratch, 0);
        Vector2 d = b - a;

        for (int i = 0; i < n; i++)
        {
            var f = _faceScratch[i];
            float c0 = Vector2.Dot(a - f.a, f.normal);      // signed distance at t = 0
            float c1 = Vector2.Dot(d, f.normal);            // how it changes along the segment

            float t0, t1;
            if (Mathf.Abs(c1) < 1e-9f)
            {
                if (c0 <= 0f) continue;                     // parallel and on the solid side throughout
                t0 = enter; t1 = exit;
            }
            else
            {
                float cross = -c0 / c1;
                if (c1 > 0f) { t0 = Mathf.Max(enter, cross); t1 = exit; }
                else { t0 = enter; t1 = Mathf.Min(exit, cross); }
            }
            at = Push(at, t0, t1);
        }
        return at;
    }

    // Sort by start, then coalesce anything touching or overlapping.
    //
    // Insertion sort on purpose: the ranges arrive from a DDA that already walks the segment in order, so all
    // that is ever out of place is the handful InsetSkin adds within one cell. That makes this a linear pass on
    // real input, where the selection sort it replaced was quadratic on every input.
    static int Merge(float[] r, int end)
    {
        for (int i = 2; i < end; i += 2)
        {
            float k0 = r[i], k1 = r[i + 1];
            int j = i - 2;
            while (j >= 0 && r[j] > k0) { r[j + 2] = r[j]; r[j + 3] = r[j + 1]; j -= 2; }
            r[j + 2] = k0;
            r[j + 3] = k1;
        }

        int w = 0;
        for (int i = 0; i < end; i += 2)
        {
            if (w > 0 && r[i] <= r[w - 1]) { if (r[i + 1] > r[w - 1]) r[w - 1] = r[i + 1]; continue; }
            r[w] = r[i];
            r[w + 1] = r[i + 1];
            w += 2;
        }
        return w;
    }

    static int Emit(WallSeg src, Vector2 a, Vector2 b, WallSeg[] into, int at)
    {
        if (into == null || at >= into.Length) return at;
        if ((a - b).sqrMagnitude < 1e-8f) return at;          // a sliver left by a clip is not a wall
        into[at] = new WallSeg { a = a, b = b, normal = src.normal, terrain = src.terrain };
        return at + 1;
    }

    // Liang-Barsky: the part of the segment inside the cell, or nothing.
    static bool ClipToRect(WallSeg seg, Rect r, out WallSeg piece)
    {
        piece = seg;
        float t0 = 0f, t1 = 1f;
        float dx = seg.b.x - seg.a.x, dy = seg.b.y - seg.a.y;

        if (!Slab(-dx, seg.a.x - r.xMin, ref t0, ref t1)) return false;
        if (!Slab(dx, r.xMax - seg.a.x, ref t0, ref t1)) return false;
        if (!Slab(-dy, seg.a.y - r.yMin, ref t0, ref t1)) return false;
        if (!Slab(dy, r.yMax - seg.a.y, ref t0, ref t1)) return false;

        piece.a = new Vector2(seg.a.x + t0 * dx, seg.a.y + t0 * dy);
        piece.b = new Vector2(seg.a.x + t1 * dx, seg.a.y + t1 * dy);
        return (piece.a - piece.b).sqrMagnitude > 1e-8f;
    }

    static bool Slab(float p, float q, ref float t0, ref float t1)
    {
        if (Mathf.Abs(p) < 1e-8f) return q >= 0f;             // parallel: inside or wholly out
        float t = q / p;
        if (p < 0f) { if (t > t1) return false; if (t > t0) t0 = t; }
        else { if (t < t0) return false; if (t < t1) t1 = t; }
        return true;
    }

    // ---- segment queries ---------------------------------------------------

    readonly WallSeg[] _queryFaces = new WallSeg[MaxFacesPerCell];

    // Does the straight line cross a wall? World space. Faces the pass mask opens are ignored, so a swimmer is
    // not stopped by a shore. passMask < 0 uses the terrain's default.
    public bool IntersectsWall(Vector3 worldA, Vector3 worldB, int passMask = -1)
    {
        if (_terrain == null) return false;
        EnsureCache();
        return SweepHitsWall(ToLocal(worldA), ToLocal(worldB), 0f,
                             passMask < 0 ? _terrain.DefaultPassMask : passMask);
    }

    // Can a body of `radius` travel straight from `from` to `to` without leaving walkable ground or clipping a
    // wall?
    //
    // `from` is NOT tested. A body already overlapping a wall has to be able to move - CollisionWorld allows that
    // overlap on purpose, and refusing the move here would put the lock back in by the other door.
    public bool CanMove(Vector3 from, Vector3 to, float radius = 0f, int passMask = -1)
    {
        if (_terrain == null) return true;
        if (passMask < 0) passMask = _terrain.DefaultPassMask;
        if (!CanPass(passMask, to)) return false;

        EnsureCache();
        return !SweepHitsWall(ToLocal(from), ToLocal(to), radius, passMask);
    }

    // The composed faces of every cell the line crosses. Amanatides-Woo, so the cost is the number of cells
    // crossed rather than the size of the map; a body with a radius also reaches into the neighbours, hence pad.
    bool SweepHitsWall(Vector2 a, Vector2 b, float radius, int passMask)
    {
        float cs = _terrain.CellSize;
        int pad = radius > 0f ? Mathf.CeilToInt(radius / cs) : 0;
        float r2 = radius * radius;

        float inv = 1f / cs;
        float ax = a.x * inv, ay = a.y * inv, bx = b.x * inv, by = b.y * inv;

        int x = Mathf.FloorToInt(ax), y = Mathf.FloorToInt(ay);
        int ex = Mathf.FloorToInt(bx), ey = Mathf.FloorToInt(by);

        float dx = bx - ax, dy = by - ay;
        int stepX = dx > 0f ? 1 : (dx < 0f ? -1 : 0);
        int stepY = dy > 0f ? 1 : (dy < 0f ? -1 : 0);

        float tDeltaX = stepX != 0 ? Mathf.Abs(1f / dx) : float.PositiveInfinity;
        float tDeltaY = stepY != 0 ? Mathf.Abs(1f / dy) : float.PositiveInfinity;
        float tMaxX = stepX > 0 ? (x + 1 - ax) * tDeltaX : (stepX < 0 ? (ax - x) * tDeltaX : float.PositiveInfinity);
        float tMaxY = stepY > 0 ? (y + 1 - ay) * tDeltaY : (stepY < 0 ? (ay - y) * tDeltaY : float.PositiveInfinity);

        int steps = Mathf.Abs(ex - x) + Mathf.Abs(ey - y) + 1;

        // A DDA moves one axis at a time, so the pad box around this cell and the one around the last overlap
        // everywhere but a single column or row. Rescanning the whole box each step composed the same cell's faces
        // 2*pad+1 times over - and a composed CellFaces is not cheap. Only what the step newly uncovered is
        // visited; the rest was already tested, and this is an any-hit query so the order never mattered.
        int opened = 0;   // 0 = the first cell, so the whole box. 1 = the column stepX opened, 2 = the row stepY did.

        for (int s = 0; s < steps; s++)
        {
            int px0 = -pad, px1 = pad, py0 = -pad, py1 = pad;
            if (opened == 1) px0 = px1 = stepX > 0 ? pad : -pad;
            else if (opened == 2) py0 = py1 = stepY > 0 ? pad : -pad;

            for (int py = py0; py <= py1; py++)
                for (int px = px0; px <= px1; px++)
                {
                    int n = CellFaces(x + px, y + py, _queryFaces, 0);
                    for (int i = 0; i < n; i++)
                    {
                        var w = _queryFaces[i];
                        if ((passMask & TerrainSet.BitOf(w.terrain)) != 0) continue;

                        bool hit = radius > 0f ? SegDistSq(a, b, w.a, w.b) < r2
                                               : SegmentsCross(a, b, w.a, w.b);
                        if (hit) return true;
                    }
                }

            if (x == ex && y == ey) break;
            if (tMaxX < tMaxY) { x += stepX; tMaxX += tDeltaX; opened = 1; }
            else { y += stepY; tMaxY += tDeltaY; opened = 2; }
        }
        return false;
    }

    // Strictly crossing: a line that only grazes an endpoint reads as clear. A shot down the exact seam between
    // two blocked cells is a tie, and calling a tie a hit would stop shots that visibly had room.
    static bool SegmentsCross(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
    {
        float d1 = Cross(q2 - q1, p1 - q1), d2 = Cross(q2 - q1, p2 - q1);
        float d3 = Cross(p2 - p1, q1 - p1), d4 = Cross(p2 - p1, q2 - p1);
        return (d1 > 0f) != (d2 > 0f) && (d3 > 0f) != (d4 > 0f);
    }

    static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;

    // Disjoint segments always meet closest at an endpoint of one of them, so four point-to-segment tests cover
    // every case including parallel.
    static float SegDistSq(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
    {
        if (SegmentsCross(p1, p2, q1, q2)) return 0f;
        float d = PointSegDistSq(p1, q1, q2);
        d = Mathf.Min(d, PointSegDistSq(p2, q1, q2));
        d = Mathf.Min(d, PointSegDistSq(q1, p1, p2));
        return Mathf.Min(d, PointSegDistSq(q2, p1, p2));
    }

    static float PointSegDistSq(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float len2 = Vector2.Dot(ab, ab);
        float t = len2 > 1e-8f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
        return (p - (a + t * ab)).sqrMagnitude;
    }

    // ---- reachability ------------------------------------------------------
    //
    // The tilemap's regions are the map without any bridge - that is what it means for it not to know they exist.
    // A bridge joins some of them, so the index here is a union-find OVER those region ids: one union per pair of
    // shores a deck touches. No new region set is built and the tilemap's own numbering is untouched.
    //
    // Cell-granular on purpose: the tilemap's regions are cells, so "which shores does this deck reach" can only
    // be answered in cells. Standing and colliding stay analytic; only this index rounds.

    int[] _union;                  // parent per tilemap region id
    int[] _volumeRoot;             // a representative region for each volume, -1 if it touches no land
    int _indexVersion = -1;

    void EnsureIndex()
    {
        if (_terrain == null) return;

        // BEFORE the version check, not after, and not left to whoever asked first. The index is built out of the
        // cache, so a stale cache does not merely delay it - it bakes the wrong answer in: every volume reads as
        // invalid, gets skipped, joins nothing, and then the stamp below says the index is current, so refreshing
        // the cache later never rebuilds it. A bridge you can walk across that Reachable says is unreachable, for
        // good. (It is also what sizes _cache, which is why five volumes and a Reachable call used to be an
        // IndexOutOfRange rather than a wrong answer.)
        EnsureCache();

        int v = WalkVersion;
        if (v == _indexVersion) return;
        _indexVersion = v;

        int n = _terrain.RegionCount;
        if (_union == null || _union.Length < n) _union = new int[Mathf.Max(n, 8)];
        for (int i = 0; i < n; i++) _union[i] = i;

        if (_volumeRoot == null || _volumeRoot.Length < _volumes.Count)
            _volumeRoot = new int[Mathf.Max(_volumes.Count, 4)];

        for (int i = 0; i < _volumes.Count; i++)
        {
            _volumeRoot[i] = -1;
            if (!_cache[i].valid) continue;

            CellRange(_cache[i].bounds, out int x0, out int y0, out int x1, out int y1);

            int first = -1;
            for (int cy = y0; cy <= y1; cy++)
                for (int cx = x0; cx <= x1; cx++)
                {
                    // A cell counts as decked when its centre is on the deck.
                    if (!_volumes[i].Contains(_terrain.CellToWorld(cx, cy))) continue;

                    // Everything walkable this decked cell touches - including itself, for a deck laid over
                    // ground that was already walkable.
                    TouchRegion(_terrain.RegionOf(cx, cy), ref first);
                    TouchRegion(_terrain.RegionOf(cx + 1, cy), ref first);
                    TouchRegion(_terrain.RegionOf(cx - 1, cy), ref first);
                    TouchRegion(_terrain.RegionOf(cx, cy + 1), ref first);
                    TouchRegion(_terrain.RegionOf(cx, cy - 1), ref first);
                }
            _volumeRoot[i] = first;
        }
    }

    void CellRange(Rect localBounds, out int x0, out int y0, out int x1, out int y1)
    {
        float cs = _terrain.CellSize;
        x0 = Mathf.FloorToInt(localBounds.xMin / cs) - 1;
        x1 = Mathf.FloorToInt(localBounds.xMax / cs) + 1;
        y0 = Mathf.FloorToInt(localBounds.yMin / cs) - 1;
        y1 = Mathf.FloorToInt(localBounds.yMax / cs) + 1;
    }

    void TouchRegion(int region, ref int first)
    {
        if (region < 0) return;
        if (first < 0) { first = region; return; }
        Union(first, region);
    }

    int Find(int a)
    {
        while (_union[a] != a) { _union[a] = _union[_union[a]]; a = _union[a]; }
        return a;
    }

    void Union(int a, int b)
    {
        int ra = Find(a), rb = Find(b);
        if (ra != rb) _union[ra] = rb;
    }

    // Could anything walk between these two points at all, however long the way round? O(1) after the index is
    // built. A raised drawbridge makes it false the moment it raises.
    public bool Reachable(Vector3 from, Vector3 to)
    {
        if (_terrain == null) return true;
        EnsureIndex();
        int a = RootAt(from), b = RootAt(to);
        return a >= 0 && a == b;
    }

    // How many stretches of ground a volume joins that are separate without it: 2 or more is a bridge, 1 is a
    // jetty, 0 means you cannot step onto it at all. Measured against the tilemap's own regions, which are always
    // the map with no bridge in it.
    public int RegionsJoined(IWalkVolume volume)
    {
        if (_terrain == null || volume == null) return 0;
        EnsureCache();   // for the space matrices, which ToLocal reads

        var wb = volume.WorldBounds;
        Vector2 b0 = ToLocal(new Vector3(wb.min.x, 0f, wb.min.z)), b1 = ToLocal(new Vector3(wb.max.x, 0f, wb.min.z));
        Vector2 b2 = ToLocal(new Vector3(wb.max.x, 0f, wb.max.z)), b3 = ToLocal(new Vector3(wb.min.x, 0f, wb.max.z));

        float lx0 = Mathf.Min(Mathf.Min(b0.x, b1.x), Mathf.Min(b2.x, b3.x));
        float lx1 = Mathf.Max(Mathf.Max(b0.x, b1.x), Mathf.Max(b2.x, b3.x));
        float ly0 = Mathf.Min(Mathf.Min(b0.y, b1.y), Mathf.Min(b2.y, b3.y));
        float ly1 = Mathf.Max(Mathf.Max(b0.y, b1.y), Mathf.Max(b2.y, b3.y));
        CellRange(new Rect(lx0, ly0, lx1 - lx0, ly1 - ly0), out int x0, out int y0, out int x1, out int y1);

        var touched = new HashSet<int>();
        for (int cy = y0; cy <= y1; cy++)
            for (int cx = x0; cx <= x1; cx++)
            {
                if (!volume.Contains(_terrain.CellToWorld(cx, cy))) continue;
                foreach (var d in Neighbourhood)
                {
                    int r = _terrain.RegionOf(cx + d.x, cy + d.y);
                    if (r >= 0) touched.Add(r);
                }
            }
        return touched.Count;
    }

    static readonly Vector2Int[] Neighbourhood =
    {
        new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(-1, 0),
        new Vector2Int(0, 1), new Vector2Int(0, -1),
    };

    // The union root of the ground under a point: its tilemap region, or - when it is standing on a deck over
    // water, where the tilemap has no region at all - the deck's.
    int RootAt(Vector3 world)
    {
        _terrain.WorldToCell(world, out int x, out int y);
        int r = _terrain.RegionOf(x, y);
        if (r >= 0) return Find(r);
        if (!_anyVolume) return -1;

        Vector2 local = ToLocal(world);
        for (int i = 0; i < _volumes.Count; i++)
        {
            if (!_cache[i].valid || !_cache[i].bounds.Contains(local)) continue;
            if (_volumes[i].Contains(world) && _volumeRoot[i] >= 0) return Find(_volumeRoot[i]);
        }
        return -1;
    }
}
