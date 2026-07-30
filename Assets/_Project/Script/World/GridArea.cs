using System;
using System.Collections.Generic;
using UnityEngine;

// A shape over the terrain, RESOLVED TO CELLS. An abstraction so nothing that uses it hard-codes a shape: a spawn
// zone picks where enemies may appear with one, and a new shape is one [Serializable] class that appears in the
// dropdown by itself.
//
// Cells are the contract (CollectCells) because that is what its consumers want - a spawn zone picks a cell to put
// something on. Free-form geometry that must NOT be rounded to cells is a different thing entirely and does not
// belong here; see BridgeShape, which a bridge uses to stay analytic.
//
// Shapes are tested in the owner's LOCAL space (y ignored, so they can sit at any height), which is what makes
// rotation free: rotate the owner and the shape rotates with it, so there is no orientation convention to
// remember and a diagonal span is just a rotated box.
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

// A run of segments with a width, the same way the river itself is described. Points are local XZ; the area is
// everything within half the width of the polyline.
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
