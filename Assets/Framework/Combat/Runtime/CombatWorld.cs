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
    static void Init() => Instance = new CombatWorld(8f);   // cell = largest combat query (AI aggro); keep queries ≤ this

    readonly SpatialHash<IDamageable> _hash;
    readonly List<IDamageable> _query = new List<IDamageable>();

    public CombatWorld(float cellSize = 4f)
        => _hash = new SpatialHash<IDamageable>(d => d.Position, cellSize);

    // Joining or leaving forces the next rebuild to be real, and that is not only about freshness: the hash's
    // buckets hold INDICES into its item list, so taking one out shifts every index behind it and the buckets
    // stop describing the world at all — they point at the wrong targets, or past the end of the list. That
    // was invisible while every caller rebuilt immediately before querying. The moment a rebuild can be
    // skipped it stops being invisible: something dying mid-frame would leave the next attack of that same
    // frame reading a scrambled index.
    public void Add(IDamageable target)
    {
        if (target == null) return;
        _hash.Add(target);
        _builtFrame = -1;
    }

    public void Remove(IDamageable target)
    {
        _hash.Remove(target);
        _builtFrame = -1;
    }

    // ONCE A FRAME, however many times it is asked for. Rebuilding costs everything hittable in the map, and
    // the AI asks once PER ENEMY — so a fight of thirty used to rebuild the whole index thirty times a frame,
    // which made the cost of a fight grow with the square of its size. Nothing at the call sites changes: they
    // still ask before they query, and asking is free after the first one.
    //
    // A FRAME NUMBER RATHER THAN A DIRTY FLAG, because what goes out of date is POSITION and everything moves
    // every frame — there is no event to hang a flag on. Once a frame is also exactly as fresh as the callers
    // were already getting: they all run after the same frame's movement.
    int _builtFrame = -1;

    public void Rebuild()
    {
        if (_builtFrame == Time.frameCount) return;
        _builtFrame = Time.frameCount;
        _hash.Rebuild();
    }

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
