using System.Collections.Generic;
using UnityEngine;
using Core;

// Circles against circles and against unwalkable terrain cells. Two rules make it behave:
//   1. Terrain resolves LAST, so walls always win and nothing is ever pushed inside one. A body
//      squeezed against a wall ends up overlapping another body - ugly for a frame, never stuck.
//   2. When a resolve is impossible, overlap is allowed. Locking the player in place is worse than
//      any visual glitch, and the death penalty makes a stuck player a lost run.
public class CollisionWorld
{
    readonly SpatialHash<ICollisionBody> _hash;
    TerrainGrid _terrain;

    public CollisionWorld(TerrainGrid terrain)
    {
        _terrain = terrain;
        float cell = terrain != null ? Mathf.Max(0.5f, terrain.CellSize * 2f) : 2f;
        _hash = new SpatialHash<ICollisionBody>(b => b.Position, cell);
    }

    // Swap the terrain when the map changes; registered bodies stay. Bodies that registered before a
    // terrain existed kept the default pass-all mask, so re-apply the new terrain's mask to all of
    // them here — otherwise a body that joined first walks through everything.
    public void SetTerrain(TerrainGrid terrain)
    {
        _terrain = terrain;
        if (terrain == null) return;

        int mask = terrain.DefaultPassMask;
        var items = _hash.Items;
        for (int i = 0; i < items.Count; i++)
            if (items[i] is CollisionBody cb) cb.SetPassMask(mask);
    }

    public void Add(ICollisionBody body)
    {
        if (body != null) _hash.Add(body);
    }

    public void Remove(ICollisionBody body) => _hash.Remove(body);

    public void Step(int iterations = 2)
    {
        // One pass is not enough: pushing out of A can push into B. A few passes converge without
        // needing a real solver.
        for (int i = 0; i < iterations; i++)
        {
            ResolveBodies();
            ResolveTerrain();
        }
    }

    void ResolveBodies()
    {
        _hash.Rebuild();
        _hash.ForEachPair(ResolvePair);
    }

    static void ResolvePair(ICollisionBody a, ICollisionBody b)
    {
        float invSum = a.InvMass + b.InvMass;
        if (invSum <= 0f) return;

        if (!Contact(a, b, out Vector3 normal, out float depth)) return;

        // normal points a -> b; separate along it by depth, split by inverse mass.
        a.Position -= normal * (depth * a.InvMass / invSum);
        b.Position += normal * (depth * b.InvMass / invSum);
    }

    // Contact between two bodies as (normal a->b, depth > 0). Dispatches on shape. Rects are AABB
    // (axis-aligned), so all three pairings are closed-form — no iteration.
    static bool Contact(ICollisionBody a, ICollisionBody b, out Vector3 normal, out float depth)
    {
        bool aRect = a.Shape == CollisionShape.Rect;
        bool bRect = b.Shape == CollisionShape.Rect;

        if (!aRect && !bRect)
            return CircleCircle(a.Position, a.Radius, b.Position, b.Radius, out normal, out depth);

        if (aRect && bRect)
            return RectRect(a.Position, a.HalfExtents, b.Position, b.HalfExtents, out normal, out depth);

        if (aRect)   // a = rect, b = circle: solve circle(b)->rect(a), then flip to a->b
        {
            bool hit = CircleRect(b.Position, b.Radius, a.Position, a.HalfExtents, out normal, out depth);
            normal = -normal;
            return hit;
        }

        // a = circle, b = rect: circle(a)->rect(b) is already a->b
        return CircleRect(a.Position, a.Radius, b.Position, b.HalfExtents, out normal, out depth);
    }

    static bool CircleCircle(Vector3 pa, float ra, Vector3 pb, float rb, out Vector3 normal, out float depth)
    {
        normal = default; depth = 0f;
        float dx = pb.x - pa.x, dz = pb.z - pa.z;
        float rSum = ra + rb;
        float d2 = dx * dx + dz * dz;
        if (d2 >= rSum * rSum || d2 < 1e-8f) return false;

        float d = Mathf.Sqrt(d2);
        normal = new Vector3(dx / d, 0f, dz / d);   // a -> b
        depth = rSum - d;
        return true;
    }

    // normal points from the CIRCLE toward the RECT.
    static bool CircleRect(Vector3 pc, float r, Vector3 pr, Vector2 he, out Vector3 normal, out float depth)
    {
        normal = default; depth = 0f;
        float dx = pc.x - pr.x, dz = pc.z - pr.z;      // rect centre -> circle centre
        float cx = Mathf.Clamp(dx, -he.x, he.x);
        float cz = Mathf.Clamp(dz, -he.y, he.y);
        float ox = dx - cx, oz = dz - cz;              // closest point on rect -> circle centre
        float d2 = ox * ox + oz * oz;

        if (d2 > r * r) return false;

        if (d2 > 1e-8f)   // circle centre outside the rect
        {
            float d = Mathf.Sqrt(d2);
            normal = new Vector3(-ox / d, 0f, -oz / d);   // circle -> rect
            depth = r - d;
            return true;
        }

        // circle centre inside the rect → eject out the nearest face
        float overlapX = he.x - Mathf.Abs(dx);
        float overlapZ = he.y - Mathf.Abs(dz);
        if (overlapX < overlapZ)
        {
            normal = new Vector3(dx < 0f ? 1f : -1f, 0f, 0f);   // circle -> rect
            depth = r + overlapX;
        }
        else
        {
            normal = new Vector3(0f, 0f, dz < 0f ? 1f : -1f);
            depth = r + overlapZ;
        }
        return true;
    }

    static bool RectRect(Vector3 pa, Vector2 ha, Vector3 pb, Vector2 hb, out Vector3 normal, out float depth)
    {
        normal = default; depth = 0f;
        float dx = pb.x - pa.x, dz = pb.z - pa.z;      // a -> b
        float ox = (ha.x + hb.x) - Mathf.Abs(dx);
        float oz = (ha.y + hb.y) - Mathf.Abs(dz);
        if (ox <= 0f || oz <= 0f) return false;

        if (ox < oz)   // least penetration on x
        {
            normal = new Vector3(dx < 0f ? -1f : 1f, 0f, 0f);   // a -> b along x
            depth = ox;
        }
        else
        {
            normal = new Vector3(0f, 0f, dz < 0f ? -1f : 1f);
            depth = oz;
        }
        return true;
    }

    // Pushes each body out of the baked walkable boundary (one-sided wall segments), skipping walls whose
    // terrain the body may pass. Walls and bodies share the terrain's local XZ plane.
    void ResolveTerrain()
    {
        var walls = _terrain != null ? _terrain.Walls : null;
        if (walls == null || walls.Length == 0) return;

        var tf = _terrain.transform;

        foreach (var body in _hash.Items)
        {
            if (body.InvMass <= 0f) continue;

            int pass = body.PassMask;
            float r = body.Radius;

            Vector3 local = tf.InverseTransformPoint(body.Position);
            Vector2 c = new Vector2(local.x, local.z);

            for (int i = 0; i < walls.Length; i++)
            {
                var w = walls[i];
                if ((pass & TerrainSet.BitOf(w.terrain)) != 0) continue;   // this body passes that terrain

                // Broad reject before the closest-point test.
                if (c.x < Mathf.Min(w.a.x, w.b.x) - r || c.x > Mathf.Max(w.a.x, w.b.x) + r ||
                    c.y < Mathf.Min(w.a.y, w.b.y) - r || c.y > Mathf.Max(w.a.y, w.b.y) + r) continue;

                Vector2 ab = w.b - w.a;
                float len2 = Vector2.Dot(ab, ab);
                float t = len2 > 1e-8f ? Mathf.Clamp01(Vector2.Dot(c - w.a, ab) / len2) : 0f;
                Vector2 d = c - (w.a + t * ab);
                float d2 = Vector2.Dot(d, d);
                if (d2 >= r * r) continue;

                // Push along the contact normal so the body slides instead of sticking; if it sits
                // exactly on the segment, push out to the walkable side.
                if (d2 > 1e-8f)
                {
                    float dist = Mathf.Sqrt(d2);
                    c += d * ((r - dist) / dist);
                }
                else c += w.normal * r;
            }

            local.x = c.x;
            local.z = c.y;
            body.Position = tf.TransformPoint(local);
        }
    }
}
