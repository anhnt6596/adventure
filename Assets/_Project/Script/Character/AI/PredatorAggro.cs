using System;
using UnityEngine;

// An ambush predator with a feeding time. Two different appetites, and the difference is not WHAT it targets
// but how far it will go for it:
//
//   Any hour  — anything that blunders into striking distance gets bitten. It does NOT commit, and an
//               uncommitted unit holds no target (EnemyAI.Reflex): it turns only to land the one bite, then
//               forgets. No follow-up, no following, not even watching them walk away.
//   Its hours — it is actively hunting. Prey spotted anywhere inside huntRadius commits it, and from there it
//               behaves like any other aggressive monster: it closes, and it does not need to be provoked.
//
// The waking window is the creature's, not this behaviour's — it lives on EnemyBrainConfig so the idle roam
// agrees with the hunt about when dawn is.
[Serializable]
public class PredatorAggro : IAggro
{
    [SerializeField] float huntRadius = 4f;   // how far it looks for prey WHILE AWAKE. Keep ≤ CombatWorld hash cell.

    public IDamageable Detect(AIContext ctx)
    {
        bool hunting = ctx.IsActiveHours;

        // Asleep it can still bite, but only what is already on top of it. Max() guards the case where someone
        // authors a hunt radius shorter than the creature's own reach — its jaws never shrink.
        float radius = hunting ? Mathf.Max(huntRadius, ctx.AttackRange) : ctx.AttackRange;

        var found = ctx.FindHostile(radius);
        if (found != null && hunting) ctx.committed = true;   // a hunt is a decision to give chase; a snap is not
        return found;
    }
}
