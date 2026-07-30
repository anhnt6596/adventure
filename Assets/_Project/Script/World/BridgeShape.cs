using System;
using UnityEngine;

// The geometry a bridge provides for itself. Free-form and analytic: authored in world units, answered by maths,
// never rasterised into cells and never handed to the tilemap. TerrainQuery reads it and nothing else does.
//
// Everything is in the SHAPE'S OWN local space (XZ, metres, origin at the bridge's transform). Bridge publishes it
// in world space, which is why rotating the transform rotates the deck and there is no orientation convention to
// remember.
//
// A shape answers exactly two questions - where it COVERS, and where its OUTLINE is - and it answers both about
// its whole self. It does NOT get to say which edges are walls: an end is open because it happens to land on
// walkable ground, and only TerrainQuery can know that. See IWalkVolume for why the two answers are enough.
//
// THE ONE INVARIANT: Outline must be exactly the boundary of Contains. An outline edge that is not on the
// boundary becomes a wall in mid-air; a stretch of boundary with no edge becomes a gap you walk off the deck
// through. Both are checkable, and a new shape is not finished until it is.
//
// That is easy for a convex shape and NOT easy otherwise: a shape built as a union of pieces has to subtract the
// other pieces' interiors from each piece's boundary, and a curved region cannot be described exactly by straight
// edges at all. A bent deck was tried as a chain of slabs and failed this on both counts (offsets cutting through
// its own middle, chords where the boundary is an arc); it needs polygonal joins that Contains agrees with, and
// it is not here until someone needs one.
[Serializable]
public abstract class BridgeShape
{
    public abstract bool Contains(Vector2 local);

    public abstract Rect LocalBounds { get; }

    // Every edge of the region's boundary, appended from `at`, with `normal` pointing inward.
    public abstract int Outline(WallSeg[] into, int at);

    public abstract int MaxOutlineEdges { get; }

    // The stretches of a->b that lie on the deck, as (t0, t1) pairs. Exact — see IWalkVolume.Overlap for why
    // this is not allowed to be sampled.
    public abstract int Overlap(Vector2 a, Vector2 b, float[] into, int at);

    public abstract int MaxOverlapValues { get; }

    protected static int Edge(WallSeg[] into, int at, Vector2 a, Vector2 b, Vector2 inward)
    {
        if (into == null || at >= into.Length) return at;
        if ((a - b).sqrMagnitude < 1e-8f) return at;
        into[at] = new WallSeg { a = a, b = b, normal = inward.normalized };
        return at + 1;
    }

    // Liang-Barsky against an axis-aligned box, in whatever frame the caller hands the segment in. A convex box
    // meets a line in exactly one interval, which is the whole reason a rectangular deck is cheap.
    protected static bool BoxRange(Vector2 a, Vector2 b, float xMin, float yMin, float xMax, float yMax,
                                   out float t0, out float t1)
    {
        t0 = 0f; t1 = 1f;
        float dx = b.x - a.x, dy = b.y - a.y;
        return Slab(-dx, a.x - xMin, ref t0, ref t1)
            && Slab(dx, xMax - a.x, ref t0, ref t1)
            && Slab(-dy, a.y - yMin, ref t0, ref t1)
            && Slab(dy, yMax - a.y, ref t0, ref t1)
            && t1 > t0;
    }

    static bool Slab(float p, float q, ref float t0, ref float t1)
    {
        if (Mathf.Abs(p) < 1e-9f) return q >= 0f;
        float t = q / p;
        if (p < 0f) { if (t > t1) return false; if (t > t0) t0 = t; }
        else { if (t < t0) return false; if (t < t1) t1 = t; }
        return true;
    }


    protected static int AddRange(float[] into, int at, float t0, float t1)
    {
        if (into == null || at + 1 >= into.Length) return at;
        if (t1 <= t0) return at;
        into[at] = t0;
        into[at + 1] = t1;
        return at + 2;
    }


#if UNITY_EDITOR
    public void DrawGizmo()
    {
        var edges = new WallSeg[Mathf.Max(1, MaxOutlineEdges)];
        int n = Outline(edges, 0);
        for (int i = 0; i < n; i++)
            Gizmos.DrawLine(new Vector3(edges[i].a.x, 0f, edges[i].a.y),
                            new Vector3(edges[i].b.x, 0f, edges[i].b.y));
    }
#endif
}

// The common bridge: a straight deck, `width` across and `length` along, centred on the transform. Both are
// floats in world units - the deck is its own geometry and owes the grid nothing, so a 1.4 unit deck is 1.4 units
// wide and not "whichever cells happened to be under it".
[Serializable]
public class DeckRect : BridgeShape
{
    [Min(0.05f)] public float width = 2f;     // across, local X
    [Min(0.05f)] public float length = 8f;    // along, local Z — the direction you cross in

    public override bool Contains(Vector2 p)
        => Mathf.Abs(p.x) <= width * 0.5f && Mathf.Abs(p.y) <= length * 0.5f;

    public override Rect LocalBounds
        => new Rect(-width * 0.5f, -length * 0.5f, width, length);

    public override int MaxOutlineEdges => 4;

    // All four. Which of them survive as walls is not this shape's business: land the ends on the shore and they
    // disappear, land them in the river and they stop you falling in.
    public override int Outline(WallSeg[] into, int at)
    {
        float hw = width * 0.5f, hl = length * 0.5f;
        at = Edge(into, at, new Vector2(hw, -hl), new Vector2(hw, hl), Vector2.left);
        at = Edge(into, at, new Vector2(-hw, -hl), new Vector2(-hw, hl), Vector2.right);
        at = Edge(into, at, new Vector2(-hw, hl), new Vector2(hw, hl), Vector2.down);
        at = Edge(into, at, new Vector2(-hw, -hl), new Vector2(hw, -hl), Vector2.up);
        return at;
    }

    public override int MaxOverlapValues => 2;

    // A box is convex: one interval, closed form, no cases.
    public override int Overlap(Vector2 a, Vector2 b, float[] into, int at)
    {
        float hw = width * 0.5f, hl = length * 0.5f;
        return BoxRange(a, b, -hw, -hl, hw, hl, out float t0, out float t1)
            ? AddRange(into, at, t0, t1) : at;
    }
}
