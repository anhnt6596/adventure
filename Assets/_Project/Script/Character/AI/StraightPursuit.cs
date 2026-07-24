using UnityEngine;

// Dumb pursuit: heads straight at the target on the ground plane, no obstacle avoidance. It'll bump into
// walls and rocks (its CollisionBody stops it sliding through) — fine for simple monsters.
public class StraightPursuit : IPursuit
{
    public Vector2 DirTo(AIContext ctx, Vector3 targetPos)
    {
        Vector3 d = targetPos - ctx.Tr.position; d.y = 0f;
        if (d.sqrMagnitude < 1e-6f) return Vector2.zero;
        d.Normalize();
        return new Vector2(d.x, d.z);
    }
}
