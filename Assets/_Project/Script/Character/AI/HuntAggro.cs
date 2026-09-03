using System;

// Comes for the player, from anywhere, forever. The arena's aggro behaviour: a survivors-like horde is not a
// world of creatures that happen to notice you, it is a tide that already knows where you are.
//
// NO RADIUS, and that is the point rather than a shortcut. SightAggro asks "is anything hostile near me",
// which is a question about a neighbourhood — and it is bounded by the combat hash cell, so it cannot be
// stretched into "anywhere" by turning a number up; past one cell the query silently finds nothing. A hunter
// asks a different question entirely, and it has a direct answer: the player.
//
// PAIR IT WITH leashRadius = -1. The FSM gives up past the leash and then re-detects this on the next Idle,
// so any finite leash turns a hunter into a stutter — chase, forget, chase — instead of a chase. -1 is the
// sentinel for "never gives up" (see EnemyBrainConfig.BeyondLeash); with it, the only thing that ends the
// hunt is the target dying.
//
// IT STILL SLEEPS. The creature's waking window (EnemyBrainConfig.activeFrom/To) is honoured, because being
// awake is a fact about the animal and hunting is only what it does once it is: a plant that folds up after
// dusk is a plant the player can go and cut down in the dark, and that is a real thing to author. "Always
// aggressive" means distance is never an argument — not that time of day isn't.
//
// UNLIKE PredatorAggro, THERE IS NO SLEEPING BITE. That one still snaps at whatever lands on top of it while
// asleep; this returns nothing at all, so a dormant hunter is genuinely safe to walk up to. Anything that
// wants the half-awake version already has PredatorAggro, and a flag here to pick between them would be the
// two behaviours merged back into one.
//
// TODO(buildings): it comes for the PLAYER. When towers and walls exist, a hunter should pick the nearest of
// the player and whatever the player built — one query over a small set, not a change to what this means.
[Serializable]
public class HuntAggro : IAggro
{
    public IDamageable Detect(AIContext ctx)
    {
        if (!ctx.IsActiveHours) return null;   // asleep — see above

        var prey = ctx.PlayerTarget;
        if (prey == null || !prey.IsAlive) return null;

        ctx.committed = true;   // it was never deciding — it was already coming
        return prey;
    }
}
