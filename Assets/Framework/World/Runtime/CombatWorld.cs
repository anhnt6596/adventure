using System.Collections.Generic;
using UnityEngine;

// Its own index, separate from collision: a ghost that collides with nothing is still hittable, and
// the rocks that fill the collision world are never a combat query's problem.
public class CombatWorld
{
    readonly SpatialHash<IDamageable> _hash;
    readonly List<IDamageable> _query = new List<IDamageable>();

    public CombatWorld(float cellSize = 4f)
        => _hash = new SpatialHash<IDamageable>(d => d.Position, cellSize);

    public void Add(IDamageable target)
    {
        if (target != null) _hash.Add(target);
    }

    public void Remove(IDamageable target) => _hash.Remove(target);

    public void Rebuild() => _hash.Rebuild();

    // Targets whose hit circle overlaps the given one, on the requested team.
    public void Overlap(Vector3 centre, float radius, int excludeTeam, List<IDamageable> results)
    {
        results.Clear();

        // The hash only compares within a cell of each other, so a reach beyond that would silently
        // miss.
        if (radius > _hash.CellSize)
            Debug.LogWarning($"[Combat] Query radius {radius} exceeds the hash cell {_hash.CellSize}; targets will be missed.");

        _hash.Query(centre, radius, _query);

        foreach (var target in _query)
        {
            if (!target.IsAlive || target.Team == excludeTeam) continue;

            Vector3 d = target.Position - centre;
            d.y = 0f;

            float reach = radius + target.HitRadius;
            if (d.x * d.x + d.z * d.z <= reach * reach) results.Add(target);
        }
    }
}
