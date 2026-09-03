using System.Collections.Generic;
using UnityEngine;

// The shared blackboard every AI state + strategy reads/writes: the unit it drives, its numbers, where it
// spawned, and its current target — plus a couple of world queries so strategies stay tiny.
public class AIContext
{
    public EnemyController controller;
    public EnemyConfig config;     // the BODY: hp, speed, damage
    public EnemyBrainConfig brain; // the MIND: this unit's own copy — FSM distances/timers and the four behaviours
    public ITimeOfDay clock;       // time of day — creatures with a body clock read it (null = always active).
                                   // ITimeOfDay, not DayNightClock: inside an arena the run's clock is the one
                                   // that means anything, and a nocturnal monster must wake by ITS night.
    public IPlayer player;         // who the run is about — hunters go straight for this rather than searching
    public Vector3 home;           // spawn position — idle behaviours orbit it
    public IDamageable target;     // current target (null = none)

    // Is this fight ON, or is the unit merely reacting? Set when the unit is struck, and by any aggro behaviour
    // that CHOOSES a target it means to go after; cleared with the target. An uncommitted unit holds no target
    // past the instant it strikes — no chase, no tracking, one turn to aim the bite and nothing after (see
    // EnemyAI.Reflex). That is the whole difference between an ambush predator's snap at whatever brushes past
    // and an actual hunt.
    public bool committed;

    // WHAT THIS CREATURE CAN DO, by the same slots a player's buttons use. A monster has no buttons, so the FSM
    // is what presses them — but it presses the same things, in the same way: an attack plan asks for the Attack
    // slot, and a plan that casts will ask for Skill1. Reading the slot rather than "the first attack component
    // on the body" is what makes that possible, and it is why a creature with two abilities can tell them apart.
    public CharacterSkill[] abilities;

    public CharacterSkill Ability(AbilitySlot slot)
    {
        if (abilities == null) return null;
        for (int i = 0; i < abilities.Length; i++)
            if (abilities[i] != null && abilities[i].Which == slot) return abilities[i];
        return null;
    }

    // The player as a thing that can be hit, or null when there is no body out there. Looked up off the live
    // body each time rather than cached: a respawn or a character switch hands out a different one, and a
    // cached reference would leave every hunter on the map chasing a corpse.
    //
    // NOT a CombatWorld search. HuntAggro means "come for the player wherever they are", and a radius query
    // cannot answer that — the hash is built for small radii and silently misses beyond one cell.
    public IDamageable PlayerTarget
    {
        get
        {
            var mc = player != null ? player.Current : null;
            return mc != null ? mc.GetComponentInChildren<Damageable>() : null;
        }
    }

    public Transform Tr => controller.transform;
    public bool HasLiveTarget => target != null && target.IsAlive;

    // Is it inside this creature's waking window? Without a clock or a brain the answer is yes: a monster with
    // no body clock is simply always awake, and every default window (0..24) says the same.
    public bool IsActiveHours => brain == null || clock == null || brain.IsActiveAt(clock.Hour);

    // Brain-owned: how close the unit gets before it stops and attacks. It is only about where the unit plants
    // its feet — what a blow can actually reach from there is the ability's own business, and a shot that flies
    // straight will miss from here if the target moves. Two numbers to keep level by eye, on purpose: a creature
    // that closed to exactly its weapon's reach would never miss, and never missing is not a monster.
    public float AttackRange => brain != null ? brain.attackRange : 0f;

    public float DistanceToTarget()
    {
        if (target == null) return Mathf.Infinity;
        Vector3 d = target.Position - Tr.position; d.y = 0f;
        return d.magnitude;
    }

    // Nearest live PREY not on this unit's team, within radius (CombatWorld query). Radius is bounded by the
    // combat hash cell — keep it small.
    //
    // The team filter alone is not enough here, and the difference matters: CombatWorld only knows "different
    // team, so the blow lands", which is true of a tree and is exactly why an axe works on one. It says nothing
    // about whether a creature should WANT to attack it. Without the IsPrey test a predator picks the nearest
    // trunk and stands there chewing bark while the player walks past.
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
            if (!Teams.IsPrey(d.Team)) continue;   // scenery is hittable, not huntable
            Vector3 v = d.Position - Tr.position; v.y = 0f;
            float sq = v.sqrMagnitude;
            if (sq < bestSqr) { bestSqr = sq; best = d; }
        }
        return best;
    }
}
