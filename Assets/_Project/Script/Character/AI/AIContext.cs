using System.Collections.Generic;
using UnityEngine;

// The shared blackboard every AI state + strategy reads/writes: the unit it drives, its numbers, where it
// spawned, and its current target — plus a couple of world queries so strategies stay tiny.
public class AIContext
{
    public EnemyController controller;
    public EnemyConfig config;     // the BODY: hp, speed, damage
    public EnemyBrainConfig brain; // the MIND: this unit's own copy — FSM distances/timers and the four behaviours
    public DayNightClock clock;    // world time — creatures with a body clock read it (null = always active)
    public Vector3 home;           // spawn position — idle behaviours orbit it
    public IDamageable target;     // current target (null = none)

    // Is this fight ON, or is the unit merely reacting? Set when the unit is struck, and by any aggro behaviour
    // that CHOOSES a target it means to go after; cleared with the target. An uncommitted unit holds no target
    // past the instant it strikes — no chase, no tracking, one turn to aim the bite and nothing after (see
    // EnemyAI.Reflex). That is the whole difference between an ambush predator's snap at whatever brushes past
    // and an actual hunt.
    public bool committed;

    public Transform Tr => controller.transform;
    public bool HasLiveTarget => target != null && target.IsAlive;

    // Is it inside this creature's waking window? Without a clock or a brain the answer is yes: a monster with
    // no body clock is simply always awake, and every default window (0..24) says the same.
    public bool IsActiveHours => brain == null || clock == null || brain.IsActiveAt(clock.Hour);

    // Brain-owned: how close the unit gets before it stops and attacks. The projectile has no range of its own
    // — once fired it homes the target wherever it goes, so this is purely the "where do I plant my feet" call.
    public float AttackRange => brain != null ? brain.attackRange : 0f;

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
