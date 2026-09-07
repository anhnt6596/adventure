using System;
using UnityEngine;

// Pursuit that goes round things. Swap it into a brain's Pursuit slot in place of StraightPursuit and that
// kind of monster stops walking into walls.
//
// SIGHT FIRST, FIELD SECOND. If the way to the target is clear it heads straight there and never touches the
// flow field — which in an open arena is nearly always, and it is what stops a navigating monster reading as
// something sliding along a grid. The field is for the case it is actually for: something in the way.
//
// THE FIELD IS NOT A PATH, so there is nothing here to store, recalculate or throw away. A body knocked
// halfway across the map is following it correctly on the very next frame, because "which way from here" is
// the only question it ever asks.
//
// KEEPING StraightPursuit IS A DESIGN DECISION, not laziness. A wall is meant to be an absolute counter to a
// dumb swarm and no obstacle at all to something that thinks — see Docs/GATE_RUN.md. If every creature could
// walk round, walls would stop meaning anything. Which monsters are clever is authored, one brain at a time.
//
// IT ONLY EVER PATHS TO WHAT IT WAS ASKED ABOUT. The field is built toward the player, so if the target is
// something else — a tower, later — this falls back to walking straight at it. That is honest rather than
// wrong: a second field, or a real path, is what that case will want, and it can be a third behaviour.
[Serializable]
public class FlowPursuit : IPursuit
{
    public Vector2 DirTo(AIContext ctx, Vector3 targetPos)
    {
        Vector3 self = ctx.Tr.position;

        Vector3 straight = targetPos - self;
        straight.y = 0f;
        Vector2 direct = straight.sqrMagnitude > 1e-6f
            ? new Vector2(straight.x, straight.z).normalized
            : Vector2.zero;

        var flow = ctx.flow;
        if (flow == null || !flow.Ready) return direct;                 // no run, no field: walk at it
        // Its OWN width, not a point: a body that fits through the gap goes straight, one that does not takes
        // the field. Without it a fat monster picks a line it will then grind along the wall of.
        if (flow.CanWalkStraight(self, targetPos, ctx.controller.BodyRadius)) return direct;

        Vector3 downhill = flow.DirectionAt(self);
        if (downhill.sqrMagnitude < 1e-6f) return direct;               // unreachable, or already there

        return new Vector2(downhill.x, downhill.z);
    }
}
