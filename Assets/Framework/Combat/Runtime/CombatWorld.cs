using System.Collections.Generic;
using UnityEngine;
using Core;

// Its own index, separate from collision: a ghost that collides with nothing is still hittable, and
// the rocks that fill the collision world are never a combat query's problem.
public class CombatWorld
{
    // The one combat index for the game, reached through Instance — a hittable self-registers (like a
    // CollisionBody with CollisionSystem) and attacks query it without being injected. Rebuilt fresh each
    // play via SubsystemRegistration, so no stale static survives across "enter play without domain reload".
    public static CombatWorld Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Init() => Instance = new CombatWorld(8f);   // cell = largest combat query (soul-fire hunt, AI aggro); keep queries ≤ this

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

    // Targets whose hit circle overlaps the given one. Team 0 is neutral and hits everyone (including
    // other team-0 targets); any other attacker team spares its own team (no friendly fire).
    public void Overlap(Vector3 centre, float radius, int attackerTeam, List<IDamageable> results)
    {
        results.Clear();

        // The hash only compares within a cell of each other, so a reach beyond that would silently
        // miss.
        if (radius > _hash.CellSize)
            Debug.LogWarning($"[Combat] Query radius {radius} exceeds the hash cell {_hash.CellSize}; targets will be missed.");

        _hash.Query(centre, radius, _query);

        foreach (var target in _query)
        {
            if (!target.IsAlive) continue;
            if (attackerTeam != 0 && target.Team == attackerTeam) continue;

            Vector3 d = target.Position - centre;
            d.y = 0f;

            float reach = radius + target.HitRadius;
            if (d.x * d.x + d.z * d.z <= reach * reach) results.Add(target);
        }
    }

    // Targets whose hit circle overlaps an ORIENTED rectangle on the ground plane — the lane a thrust or a
    // cleave sweeps. `forward` is the rect's local +Z (the attacker's facing; it need not be normalised),
    // halfLength runs along it and halfWidth across it, both measured from `centre`. Same team rule as Overlap.
    public void OverlapBox(Vector3 centre, Vector3 forward, float halfWidth, float halfLength, int attackerTeam, List<IDamageable> results)
    {
        results.Clear();

        float hw = Mathf.Max(0f, halfWidth), hl = Mathf.Max(0f, halfLength);

        // The corner is the rect's furthest point, so that — not an edge — is what the broad phase must cover
        // and what the cell has to fit.
        float bounding = Mathf.Sqrt(hw * hw + hl * hl);
        if (bounding > _hash.CellSize)
            Debug.LogWarning($"[Combat] Query reach {bounding} exceeds the hash cell {_hash.CellSize}; targets will be missed.");

        Vector3 fwd = forward;
        fwd.y = 0f;
        fwd = fwd.sqrMagnitude > 1e-6f ? fwd.normalized : Vector3.forward;
        Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);   // +90° about Y, so (0,0,1) reads as (1,0,0)

        _hash.Query(centre, bounding, _query);

        foreach (var target in _query)
        {
            if (!target.IsAlive) continue;
            if (attackerTeam != 0 && target.Team == attackerTeam) continue;

            Vector3 d = target.Position - centre;
            d.y = 0f;

            // Into the rect's own frame, then clamp onto it: what's left is the gap from the target to the
            // nearest point ON the rect, which its hit circle has to cover to count. Inside the rect the
            // clamp is a no-op and the gap is zero, so containment falls out of the same test.
            float lx = Vector3.Dot(d, right), lz = Vector3.Dot(d, fwd);
            float gx = lx - Mathf.Clamp(lx, -hw, hw);
            float gz = lz - Mathf.Clamp(lz, -hl, hl);

            if (gx * gx + gz * gz <= target.HitRadius * target.HitRadius) results.Add(target);
        }
    }
}
