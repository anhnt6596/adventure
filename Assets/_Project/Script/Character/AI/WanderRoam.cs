using UnityEngine;

// Idle roam: picks a random spot within config.wanderRadius of the spawn, walks straight to it, then stands
// and rests a few seconds before choosing the next one. Committing to a fixed point (instead of re-aiming
// every frame) keeps the heading — and the sprite's facing — steady: it strolls, pauses, strolls, with a
// single clean turn at each new spot. Per-instance state, so each unit roams on its own schedule.
public class WanderRoam : IIdleBehavior
{
    const float Amble = 0.5f;        // fraction of full speed while strolling
    const float Arrive = 0.3f;       // close enough to the spot to call it arrived
    const float RestMin = 1.5f;      // pause range between strolls
    const float RestMax = 3.5f;

    Vector3 _dest;
    bool _hasDest;
    float _rest;

    public void Tick(AIContext ctx)
    {
        if (_rest > 0f) { _rest -= Time.deltaTime; return; }   // resting — stand still, hold facing

        if (!_hasDest) PickDest(ctx);

        Vector3 to = _dest - ctx.Tr.position; to.y = 0f;
        if (to.sqrMagnitude <= Arrive * Arrive)
        {
            _hasDest = false;
            _rest = Random.Range(RestMin, RestMax);   // arrived — rest, then head somewhere new
            return;
        }

        to.Normalize();
        ctx.controller.Move(new Vector2(to.x, to.z) * Amble);
    }

    void PickDest(AIContext ctx)
    {
        // Try a few random spots in range; reject water / off-map so the roam stays on land (the frog spawned
        // on a baked walkable cell, so home is safe). If every pick lands off it — e.g. spawned by a pond edge —
        // just hold at home this round rather than walking into the water.
        for (int i = 0; i < 8; i++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            if (dir == Vector2.zero) dir = Vector2.up;
            float dist = Random.Range(0.4f, 1f) * ctx.config.wanderRadius;
            Vector3 spot = ctx.home + new Vector3(dir.x, 0f, dir.y) * dist;

            if (CollisionSystem.Instance == null || CollisionSystem.Instance.IsWalkable(spot))
            {
                _dest = spot;
                _hasDest = true;
                return;
            }
        }

        _dest = ctx.home;
        _hasDest = true;
    }
}
