using System;
using System.Collections.Generic;
using UnityEngine;

// A shape over the terrain. An abstraction so nothing that uses it hard-codes a shape - a spawn zone picks where
// enemies may appear with one, a bridge picks which cells it decks with the same one, and a new shape is one
// [Serializable] class that appears in the dropdown by itself.
//
// WHAT EVERY CONSUMER ACTUALLY WANTS IS CELLS, so that is the contract: CollectCells. Continuous shapes get it for
// free - the default rasterises Contains over the grid, testing each cell's CENTRE - while a shape authored in
// tiles overrides it and hands back its cells exactly, with no rasterisation to be surprised by. That distinction
// is the whole reason the method exists rather than everyone calling Contains: a tile-measured span should give
// you the tiles you asked for, not the tiles whose centres happened to land inside a rectangle.
//
// Continuous shapes are tested in the owner's LOCAL space (y ignored, so they can sit at any height), which is
// what makes rotation free: rotate the owner and the shape rotates with it, so there is no orientation convention
// to remember and a diagonal bridge is just a rotated box.
//
// [SerializeReference] on the owner keeps the chosen concrete type serialized. Renaming a CONCRETE class breaks
// those references (the class name is what gets stored) - rename one and it needs [MovedFrom]. Renaming this
// abstract base is safe: it is never the stored name.
[Serializable]
public abstract class GridArea
{
    // True if this local-space point is inside. Only the default CollectCells calls it, so a shape that
    // enumerates its own cells never has to answer it meaningfully.
    public abstract bool Contains(Vector3 local);

    // False for a shape measured in whole cells: a rotated set of cells is not a cell rectangle, so the owner's
    // rotation is ignored — and the gizmo has to be drawn unrotated too, or the outline lies about the cells.
    public virtual bool UsesRotation => true;

    // The cells this area covers. Default: every cell whose centre falls inside.
    public virtual void CollectCells(TerrainGrid grid, Transform owner, List<Vector2Int> into)
    {
        if (grid == null || owner == null) return;
        for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
                if (Contains(owner.InverseTransformPoint(grid.CellToWorld(x, y))))
                    into.Add(new Vector2Int(x, y));
    }

#if UNITY_EDITOR
    // Drawn in local space — the caller sets Gizmos.matrix to the owner's transform. cellSize is passed because a
    // shape may be measured in cells and cannot draw itself without it.
    public abstract void DrawGizmo(float cellSize);
#endif
}

// A span measured in TILES: exactly size.x by size.y cells, no rasterisation involved. The natural way to author a
// bridge deck, because the deck is a set of cells in the end - picking world units and then squinting at which
// cell centres landed inside is double work, and it is where "the yellow cells look offset from the outline" comes
// from.
//
// Anchored on the cell under the owner and grown from there, so an odd size centres exactly on it and an even one
// leans one cell toward -x/-z. Rotation is IGNORED: a rotated set of whole cells is not a cell rectangle, and
// pretending otherwise would quietly stop giving you the size you asked for. Diagonal spans want BoxArea (which
// rotates) or LineArea.
[Serializable]
public class TileRectArea : GridArea
{
    public Vector2Int size = new Vector2Int(1, 4);   // cells across (grid X) and along (grid Z)

    public override bool UsesRotation => false;

    public override void CollectCells(TerrainGrid grid, Transform owner, List<Vector2Int> into)
    {
        if (grid == null || owner == null) return;

        grid.WorldToCell(owner.position, out int cx, out int cy);   // coords are valid even off-map
        int w = Mathf.Max(1, size.x), h = Mathf.Max(1, size.y);
        int x0 = cx - (w - 1) / 2, y0 = cy - (h - 1) / 2;

        for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
                into.Add(new Vector2Int(x, y));
    }

    // Never consulted: CollectCells is overridden, and a tile count cannot answer a continuous point test without
    // knowing how big a cell is.
    public override bool Contains(Vector3 local) => false;

#if UNITY_EDITOR
    public override void DrawGizmo(float cellSize)
    {
        int w = Mathf.Max(1, size.x), h = Mathf.Max(1, size.y);
        // Drawn about the owner the same way the cells are anchored, so the outline sits where the tiles will.
        var offset = new Vector3(((w - 1) % 2) * 0.5f * cellSize, 0f, ((h - 1) % 2) * 0.5f * cellSize);
        Gizmos.DrawWireCube(offset, new Vector3(w * cellSize, 0f, h * cellSize));
    }
#endif
}

[Serializable]
public class CircleArea : GridArea
{
    [Min(0f)] public float radius = 5f;

    public override bool Contains(Vector3 local)
    {
        local.y = 0f;
        return local.sqrMagnitude <= radius * radius;
    }

#if UNITY_EDITOR
    public override void DrawGizmo(float cellSize)
    {
        const int seg = 48;
        Vector3 prev = new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg;
            Vector3 next = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
#endif
}

[Serializable]
public class BoxArea : GridArea
{
    public Vector2 size = new Vector2(10f, 10f);   // x = width, y = depth (local Z)

    public override bool Contains(Vector3 local)
        => Mathf.Abs(local.x) <= size.x * 0.5f && Mathf.Abs(local.z) <= size.y * 0.5f;

#if UNITY_EDITOR
    public override void DrawGizmo(float cellSize) => Gizmos.DrawWireCube(Vector3.zero, new Vector3(size.x, 0f, size.y));
#endif
}

// A run of segments with a width: the shape a bridge wants when the crossing bends, and the same way the river
// itself is described. Points are local XZ; the area is everything within half the width of the polyline.
[Serializable]
public class LineArea : GridArea
{
    public Vector2[] points = { new Vector2(0f, -5f), new Vector2(0f, 5f) };
    [Min(0.1f)] public float width = 4f;

    public override bool Contains(Vector3 local)
    {
        if (points == null || points.Length == 0) return false;

        var p = new Vector2(local.x, local.z);
        float half = width * 0.5f;
        if (points.Length == 1) return (p - points[0]).sqrMagnitude <= half * half;

        for (int i = 0; i < points.Length - 1; i++)
            if (DistanceSqToSegment(p, points[i], points[i + 1]) <= half * half) return true;
        return false;
    }

    static float DistanceSqToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float len2 = Vector2.Dot(ab, ab);
        float t = len2 > 1e-8f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
        return (p - (a + t * ab)).sqrMagnitude;
    }

#if UNITY_EDITOR
    public override void DrawGizmo(float cellSize)
    {
        if (points == null || points.Length == 0) return;

        float half = width * 0.5f;
        for (int i = 0; i < points.Length - 1; i++)
        {
            Vector3 a = new Vector3(points[i].x, 0f, points[i].y);
            Vector3 b = new Vector3(points[i + 1].x, 0f, points[i + 1].y);
            Gizmos.DrawLine(a, b);

            // The edges of the band, so the authored width is visible and not just a number.
            Vector3 n = Vector3.Cross(Vector3.up, (b - a).normalized) * half;
            Gizmos.DrawLine(a + n, b + n);
            Gizmos.DrawLine(a - n, b - n);
        }
        foreach (var pt in points)
            Gizmos.DrawWireSphere(new Vector3(pt.x, 0f, pt.y), half);
    }
#endif
}
