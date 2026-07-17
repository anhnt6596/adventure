using System.Collections.Generic;
using UnityEngine;

// Circles against circles and against unwalkable terrain cells. Two rules make it behave:
//   1. Terrain resolves LAST, so walls always win and nothing is ever pushed inside one. A body
//      squeezed against a wall ends up overlapping another body - ugly for a frame, never stuck.
//   2. When a resolve is impossible, overlap is allowed. Locking the player in place is worse than
//      any visual glitch, and the death penalty makes a stuck player a lost run.
public class CollisionWorld
{
    readonly List<ICollisionBody> _bodies = new List<ICollisionBody>();
    readonly Dictionary<long, List<int>> _buckets = new Dictionary<long, List<int>>();
    readonly Stack<List<int>> _bucketPool = new Stack<List<int>>();

    TerrainGrid _terrain;
    float _bucketSize = 2f;

    public CollisionWorld(TerrainGrid terrain)
    {
        _terrain = terrain;
        if (terrain != null) _bucketSize = Mathf.Max(0.5f, terrain.CellSize * 2f);
    }

    public void Add(ICollisionBody body)
    {
        if (body != null && !_bodies.Contains(body)) _bodies.Add(body);
    }

    public void Remove(ICollisionBody body) => _bodies.Remove(body);

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
        RebuildBuckets();

        foreach (var cell in _buckets)
        {
            var list = cell.Value;
            for (int a = 0; a < list.Count; a++)
                for (int b = a + 1; b < list.Count; b++)
                    ResolvePair(_bodies[list[a]], _bodies[list[b]]);

            // Neighbour buckets: only forward (+x, +z and the two diagonals) so each pair is seen once.
            long key = cell.Key;
            int cx = (int)(key >> 32), cz = (int)(key & 0xFFFFFFFF);
            ResolveAgainst(list, cx + 1, cz);
            ResolveAgainst(list, cx, cz + 1);
            ResolveAgainst(list, cx + 1, cz + 1);
            ResolveAgainst(list, cx + 1, cz - 1);
        }
    }

    void ResolveAgainst(List<int> list, int cx, int cz)
    {
        if (!_buckets.TryGetValue(Key(cx, cz), out var other)) return;
        for (int a = 0; a < list.Count; a++)
            for (int b = 0; b < other.Count; b++)
                ResolvePair(_bodies[list[a]], _bodies[other[b]]);
    }

    static void ResolvePair(ICollisionBody a, ICollisionBody b)
    {
        float invSum = a.InvMass + b.InvMass;
        if (invSum <= 0f) return;

        Vector3 delta = b.Position - a.Position;
        delta.y = 0f;

        float rSum = a.Radius + b.Radius;
        float dist2 = delta.x * delta.x + delta.z * delta.z;
        if (dist2 >= rSum * rSum || dist2 < 1e-8f) return;

        float dist = Mathf.Sqrt(dist2);
        Vector3 normal = delta / dist;
        float depth = rSum - dist;

        a.Position -= normal * (depth * a.InvMass / invSum);
        b.Position += normal * (depth * b.InvMass / invSum);
    }

    void ResolveTerrain()
    {
        if (_terrain == null) return;

        for (int i = 0; i < _bodies.Count; i++)
        {
            var body = _bodies[i];
            if (body.InvMass <= 0f) continue;
            body.Position = ResolveTerrain(body.Position, body.Radius, body.PassMask);
        }
    }

    Vector3 ResolveTerrain(Vector3 world, float radius, int passMask)
    {
        var tf = _terrain.transform;
        Vector3 local = tf.InverseTransformPoint(world);
        float cs = _terrain.CellSize;

        int x0 = Mathf.FloorToInt((local.x - radius) / cs);
        int x1 = Mathf.FloorToInt((local.x + radius) / cs);
        int z0 = Mathf.FloorToInt((local.z - radius) / cs);
        int z1 = Mathf.FloorToInt((local.z + radius) / cs);

        for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                if (_terrain.CanPass(passMask, x, z)) continue;

                float minX = x * cs, maxX = minX + cs;
                float minZ = z * cs, maxZ = minZ + cs;

                float nearX = Mathf.Clamp(local.x, minX, maxX);
                float nearZ = Mathf.Clamp(local.z, minZ, maxZ);

                float dx = local.x - nearX;
                float dz = local.z - nearZ;
                float d2 = dx * dx + dz * dz;

                if (d2 >= radius * radius) continue;

                if (d2 > 1e-8f)
                {
                    // Pushing along the contact normal keeps the tangential motion, so a body slides
                    // along a wall instead of sticking to it.
                    float d = Mathf.Sqrt(d2);
                    float push = radius - d;
                    local.x += dx / d * push;
                    local.z += dz / d * push;
                }
                else
                {
                    local = EjectFromCell(local, minX, maxX, minZ, maxZ, radius);
                }
            }

        return tf.TransformPoint(local);
    }

    // Centre is inside the cell: no contact normal exists, so leave by the nearest face.
    static Vector3 EjectFromCell(Vector3 local, float minX, float maxX, float minZ, float maxZ, float radius)
    {
        float left = local.x - minX, right = maxX - local.x;
        float down = local.z - minZ, up = maxZ - local.z;

        float best = left;
        int face = 0;
        if (right < best) { best = right; face = 1; }
        if (down < best) { best = down; face = 2; }
        if (up < best) { best = up; face = 3; }

        switch (face)
        {
            case 0: local.x = minX - radius; break;
            case 1: local.x = maxX + radius; break;
            case 2: local.z = minZ - radius; break;
            default: local.z = maxZ + radius; break;
        }
        return local;
    }

    void RebuildBuckets()
    {
        foreach (var list in _buckets.Values)
        {
            list.Clear();
            _bucketPool.Push(list);
        }
        _buckets.Clear();

        for (int i = 0; i < _bodies.Count; i++)
        {
            var p = _bodies[i].Position;
            long key = Key(Mathf.FloorToInt(p.x / _bucketSize), Mathf.FloorToInt(p.z / _bucketSize));
            if (!_buckets.TryGetValue(key, out var list))
            {
                list = _bucketPool.Count > 0 ? _bucketPool.Pop() : new List<int>();
                _buckets[key] = list;
            }
            list.Add(i);
        }
    }

    static long Key(int x, int z) => ((long)x << 32) | (uint)z;
}
