using System;
using UnityEngine;

// Idle roam: picks a random spot within radius of the spawn, walks straight to it, then stands and rests a few
// seconds before choosing the next one. Committing to a fixed point (instead of re-aiming every frame) keeps the
// heading — and the sprite's facing — steady: it strolls, pauses, strolls, with a single clean turn at each new
// spot. Per-instance state, so each unit roams on its own schedule (EnemyAI copies the brain per unit — without
// that copy a whole pack would share one destination and one timer).
//
// KEEPING ITS DISTANCE LIVES HERE TOO, and not in a behaviour of its own, because moving off means dropping the
// spot it was walking to and the rest it was taking — and those are this class's own state. Split across a
// wrapper and the animal would move away, then calmly resume walking toward a point it chose before, which on a
// small radius is usually straight back at whatever it just moved away from. `personalSpace = 0` switches the
// whole thing off, so nothing that does not mind company pays for it.
//
// IT IS NOT A PANIC. It ambles off at exactly the speed it strolls at — the only thing that changed is WHERE it
// decided to go. A creature that breaks into a run reads as prey fleeing a predator; this one reads as something
// that would rather not be crowded, which is what it is. That is also why there is no separate speed to tune:
// the one right value was always `amble`, so it is `amble`.
[Serializable]
public class WanderRoam : IIdleBehavior
{
    [SerializeField] float radius = 3f;        // how far from the spawn point it is willing to amble
    [SerializeField] float amble = 0.5f;       // fraction of full speed while strolling
    [SerializeField] float restMin = 1.5f;     // pause range between strolls
    [SerializeField] float restMax = 3.5f;
    [SerializeField] bool activeHoursOnly;     // stand dead still outside the creature's waking window (EnemyBrainConfig's). A long rest range on top of this is what makes something that only stirs now and then, at dawn.

    [Header("Personal space")]
    [Tooltip("0 = does not mind company. How close something on another side gets before this quietly moves off. " +
             "Keep it small — like every other AI radius it must stay within the CombatWorld hash cell.")]
    [SerializeField] float personalSpace;
    [SerializeField] float stepAside = 2.5f;    // how far it moves off — one unhurried walk, not a bolt

    // How soon it will give ground AGAIN. Short on purpose: keep walking at it and it keeps drifting away, which
    // is what "it would rather not be next to you" looks like. Long, and it yields once and then stands there
    // being crowded, which reads as broken rather than as calm. What keeps the whole thing rare is the small
    // personalSpace, not this.
    [SerializeField] float settleTime = 0.5f;

    const float Arrive = 0.3f;                 // close enough to the spot to call it arrived — not a tuning knob
    const int Tries = 8;                        // how many candidate spots to test before giving up on a pick

    // How often it may LOOK, as opposed to how often it may move. settleTime only starts once something has
    // actually crowded it, so without this an animal standing in an empty field would run a CombatWorld scan
    // every frame for ever — and that scan rebuilds the spatial hash. Four times a second is far below anything
    // an unhurried step aside needs to look responsive. Not a tuning knob: it is a poll rate, not a trait.
    const float ScanInterval = 0.25f;

    Vector3 _dest;
    bool _hasDest;
    float _rest;
    float _settle;
    float _scan;

    public void Tick(AIContext ctx)
    {
        _settle -= Time.deltaTime;
        _scan -= Time.deltaTime;

        // Off-hours: stop where it is and drop the destination, so waking up picks a fresh spot rather than
        // resuming a walk it started hours ago toward somewhere nothing is any more. A sleeping animal does not
        // mind being crowded either — that is what makes a night creature safe to walk past in daylight.
        if (activeHoursOnly && !ctx.IsActiveHours) { _hasDest = false; return; }

        // Checked before the rest timer, or a resting animal would sit there while something walked up to it.
        // settleTime, set the moment it decides to move, is also what keeps it from re-deciding on the way.
        if (personalSpace > 0f && _settle <= 0f && _scan <= 0f) { _scan = ScanInterval; MakeWay(ctx); }

        if (_rest > 0f) { _rest -= Time.deltaTime; return; }   // resting — stand still, hold facing

        if (!_hasDest) PickDest(ctx);

        Vector3 to = _dest - ctx.Tr.position; to.y = 0f;
        if (to.sqrMagnitude <= Arrive * Arrive)
        {
            _hasDest = false;
            _rest = UnityEngine.Random.Range(restMin, restMax);   // arrived — rest, then head somewhere new
            return;
        }

        // One speed, whether it chose the spot to stretch its legs or to get out of someone's way. From here down
        // there is no such thing as "moving off" — it is the same stroll to the same kind of committed point.
        to.Normalize();
        ctx.controller.Move(new Vector2(to.x, to.z) * amble);
    }

    // Something on another side came close: pick somewhere further off and go there. All this does is choose a
    // different destination — the walking is the same walking, which is the whole point.
    //
    // FindHostile is the same query a predator hunts with, read the other way round — "anything alive on another
    // side, that is not scenery". A frog has no use for the distinction between a wolf and a player; both are
    // something large it would rather not be next to.
    void MakeWay(AIContext ctx)
    {
        var other = ctx.FindHostile(personalSpace);
        if (other == null) return;

        Vector3 away = ctx.Tr.position - other.Position; away.y = 0f;
        if (away.sqrMagnitude < 0.0001f) away = UnityEngine.Random.insideUnitSphere;   // stood on top of it
        away.y = 0f;
        away.Normalize();

        _settle = settleTime;   // spent whether or not a spot is found: an animal with nowhere to go should not re-scan every frame
        _rest = 0f;

        // Straight away first, then wider and wider off it — something with its back to a wall still gets to
        // sidestep, and only an animal boxed in on every side stays put.
        for (int i = 0; i < Tries; i++)
        {
            // 0, then -30, +30, -60, +60 ... — straight away first, then alternating to either side.
            float spread = ((i + 1) / 2) * 30f * Mathf.Deg2Rad * ((i & 1) == 0 ? 1f : -1f);
            Vector3 dir = new Vector3(away.x * Mathf.Cos(spread) - away.z * Mathf.Sin(spread), 0f,
                                      away.x * Mathf.Sin(spread) + away.z * Mathf.Cos(spread));
            Vector3 spot = ctx.Tr.position + dir * stepAside;
            if (!Standable(ctx, spot)) continue;

            _dest = spot;
            _hasDest = true;
            return;
        }
    }

    void PickDest(AIContext ctx)
    {
        // Try a few random spots in range; reject water / off-map so the roam stays on land (the frog spawned
        // on a baked walkable cell, so home is safe). If every pick lands off it — e.g. spawned by a pond edge —
        // just hold at home this round rather than walking into the water.
        for (int i = 0; i < Tries; i++)
        {
            Vector2 dir = UnityEngine.Random.insideUnitCircle.normalized;
            if (dir == Vector2.zero) dir = Vector2.up;
            float dist = UnityEngine.Random.Range(0.4f, 1f) * radius;
            Vector3 spot = ctx.home + new Vector3(dir.x, 0f, dir.y) * dist;

            if (Standable(ctx, spot))
            {
                _dest = spot;
                _hasDest = true;
                return;
            }
        }

        _dest = ctx.home;
        _hasDest = true;
    }

    // Somewhere it can stand AND somewhere it can get to. Walkable alone is not enough: the far bank of a river
    // is perfectly walkable, and a unit that picks a spot over there walks into the near bank and stays there
    // shoving until the rest timer saves it. Reachable answers that in O(1) off the connected components, and it
    // counts bridges — so a crossing that opens makes the far bank a legal destination the moment it opens, and a
    // drawbridge raising takes it away again.
    static bool Standable(AIContext ctx, Vector3 spot)
    {
        var cs = CollisionSystem.Instance;
        if (cs == null) return true;   // no collision world (a test scene) -> no restriction
        return cs.IsWalkable(spot) && cs.Reachable(ctx.Tr.position, spot);
    }
}
