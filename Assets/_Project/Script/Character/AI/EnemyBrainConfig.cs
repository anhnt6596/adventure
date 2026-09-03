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
    [Tooltip("Give up the chase past this distance.\n\n" +
             "-1 = NEVER give up. An arena monster is a tide, not an animal defending a patch: it is coming, " +
             "and distance is not an argument. Use the sentinel rather than a huge number — 'never' is a " +
             "different statement from 'nine hundred', and a number that big invites somebody to wonder " +
             "whether it is a bug.")]
    public float leashRadius = 8f;
    public float reEngageRadius = 5f;   // in Forget, resume if the target comes back within this
    public float forgetTime = 3f;       // seconds standing still before returning to idle
    public float recognizeTime = 1f;    // reaction delay: on first turning aggressive (spotted / got hit), freeze in Idle this long before engaging
    public float retaliateRadius = 4f;  // hit by something it can't identify -> look this far for the culprit. NOT sight range (that's SightAggro's); even a blind passive monster needs it. Keep ≤ CombatWorld hash cell.

    // The creature's waking window, in hours of the day. It is a trait of the ANIMAL, not of any one behaviour,
    // which is why it sits here: a predator plant that only hunts at dawn wants its roaming AND its hunting to
    // agree on when dawn is, and two copies of the number would drift. Behaviours read it via ctx.IsActiveHours.
    // The default 0..24 means always awake, so a monster that doesn't care never notices this exists.
    [Header("Body clock")]
    public float activeFrom = 0f;
    public float activeTo = 24f;

    // Wraps past midnight when the window runs backwards (a night creature: 20 -> 4).
    public bool IsActiveAt(float hour)
    {
        if (activeFrom == activeTo) return true;                          // degenerate window -> always awake
        if (activeTo > activeFrom) return hour >= activeFrom && hour < activeTo;
        return hour >= activeFrom || hour < activeTo;
    }

    public bool HasBodyClock => activeFrom != activeTo && !(activeFrom <= 0f && activeTo >= 24f);

    // Has the target got far enough away to be dropped? The one place the -1 sentinel is read, so nothing
    // else has to remember it exists — a comparison written out by hand somewhere would quietly treat "never"
    // as "at once", which is the exact opposite and would look like the monster being broken.
    public bool BeyondLeash(float distance) => leashRadius >= 0f && distance > leashRadius;

    public bool IsComplete => idle != null && aggro != null && pursuit != null && attack != null;
}
