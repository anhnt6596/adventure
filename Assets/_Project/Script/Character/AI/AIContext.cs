using System.Collections.Generic;
using UnityEngine;

// The shared blackboard every AI state + strategy reads/writes: the unit it drives, its numbers, where it
// spawned, and its current target — plus a couple of world queries so strategies stay tiny.
public class AIContext
{
    public EnemyController controller;
    public EnemyConfig config;
    public Vector3 home;           // spawn position — idle behaviours orbit it
    public IDamageable target;     // current target (null = none)

    public Transform Tr => controller.transform;
    public bool HasLiveTarget => target != null && target.IsAlive;

    // Brain-owned: how close the unit gets before it stops and attacks. The projectile has no range of its own
    // — once fired it homes the target wherever it goes, so this is purely the "where do I plant my feet" call.
    public float AttackRange => config != null ? config.attackRange : 0f;

    public float DistanceToTarget()
    {
        if (target == null) return Mathf.Infinity;
        Vector3 d = target.Position - Tr.position; d.y = 0f;
        return d.magnitude;
    }

    // Nearest live hittable NOT on this unit's team, within radius (CombatWorld query). Props share the enemy
    // team, so a tree is never returned. Radius is bounded by the combat hash cell — keep it small.
    static readonly List<IDamageable> _buf = new List<IDamageable>();
    public IDamageable FindHostile(float radius)
    {
        CombatWorld.Instance.Rebuild();
        CombatWorld.Instance.Overlap(Tr.position, radius, controller.Team, _buf);

        IDamageable best = null;
        float bestSqr = float.MaxValue;
        foreach (var d in _buf)
        {
            if (d == null || !d.IsAlive) continue;
            Vector3 v = d.Position - Tr.position; v.y = 0f;
            float sq = v.sqrMagnitude;
            if (sq < bestSqr) { bestSqr = sq; best = d; }
        }
        return best;
    }
}
