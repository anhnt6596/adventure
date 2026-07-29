using System;
using UnityEngine;

// Proactive sight: anything hostile that comes within radius gets attacked, unprovoked. The counterpart to
// PassiveAggro — swap this into a brain's Aggro slot to turn a timid creature into an ambusher without touching
// any code.
//
// Radius is the behaviour's own number, not a shared stat: a monster that never looks around has no business
// carrying a sight range. Keep it at or below the CombatWorld hash cell or the query silently misses (it warns).
[Serializable]
public class SightAggro : IAggro
{
    [SerializeField] float radius = 4f;

    public IDamageable Detect(AIContext ctx)
    {
        var found = ctx.FindHostile(radius);
        if (found != null) ctx.committed = true;   // seeing it and picking it IS the decision to go after it
        return found;
    }
}
