using UnityEngine;

// Everything about how a kind of monster THINKS, in one asset: which of the four behaviours it runs, each
// behaviour's own tuning, and the FSM skeleton's own numbers. This replaces the old one-subclass-per-monster
// arrangement (MewFrogAI etc.) where the four picks were hard-coded — which is why PP1, a melee plant, shipped
// running the timid ranged frog's brain: writing a class was the only way to differ, so nobody bothered.
//
// The split against EnemyConfig is by OWNER, not by convenience: EnemyConfig is the BODY (hp, speed, damage —
// what the unit is), this is the MIND (what it decides — how far it chases, when it gives up, how it roams).
// Two kinds with identical stats can think differently, and a brain can be reused across kinds.
//
// Deliberately NO logic and NO methods here beyond the completeness check. The algorithms stay plain C#
// classes; this asset only records which instances a kind carries. [SerializeReference] is what makes that
// possible — it serializes the real C# object (type included), so a slot both PICKS the behaviour and holds
// that behaviour's tuning, on one screen.
//
// Shared per kind, so EnemyAI takes a COPY per unit — see EnemyAI.BuildBrain. Never mutate one at runtime.
[CreateAssetMenu(menuName = "Config/Enemy Brain")]
public class EnemyBrainConfig : ScriptableObject
{
    [SerializeReference] public IIdleBehavior idle;      // what it does with no target
    [SerializeReference] public IAggro aggro;            // whether/how it picks a fight
    [SerializeReference] public IPursuit pursuit;        // how it closes the distance
    [SerializeReference] public IAttackPlan attack;      // when it pulls the trigger

    // The FSM skeleton's own numbers — EnemyAI reads these whatever behaviours are plugged in. A number only
    // ONE behaviour cares about (roam radius, sight range) does not belong here; it lives on that behaviour,
    // where a kind that doesn't use the behaviour never has to carry it.
    [Header("FSM")]
    public float attackRange = 3f;      // stop and fire within this of the target — where it plants its feet, not the weapon's reach
    public float leashRadius = 8f;      // give up the chase past this distance
    public float reEngageRadius = 5f;   // in Forget, resume if the target comes back within this
    public float forgetTime = 3f;       // seconds standing still before returning to idle
    public float recognizeTime = 1f;    // reaction delay: on first turning aggressive (spotted / got hit), freeze in Idle this long before engaging
    public float retaliateRadius = 4f;  // hit by something it can't identify -> look this far for the culprit. NOT sight range (that's SightAggro's); even a blind passive monster needs it. Keep ≤ CombatWorld hash cell.

    public bool IsComplete => idle != null && aggro != null && pursuit != null && attack != null;
}
