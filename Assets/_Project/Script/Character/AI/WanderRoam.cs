using System;
using UnityEngine;

// Idle roam: picks a random spot within radius of the spawn, walks straight to it, then stands and rests a few
// seconds before choosing the next one. Committing to a fixed point (instead of re-aiming every frame) keeps the
// heading — and the sprite's facing — steady: it strolls, pauses, strolls, with a single clean turn at each new
// spot. Per-instance state, so each unit roams on its own schedule (EnemyAI copies the brain per unit — without
// that copy a whole pack would share one destination and one timer).
[Serializable]
public class WanderRoam : IIdleBehavior
{
    [SerializeField] float radius = 3f;        // how far from the spawn point it is willing to amble
    [SerializeField] float amble = 0.5f;       // fraction of full speed while strolling
    [SerializeField] float restMin = 1.5f;     // pause range between strolls
    [SerializeField] float restMax = 3.5f;

    const float Arrive = 0.3f;                 // close enough to the spot to call it arrived — not a tuning knob

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
            _rest = UnityEngine.Random.Range(restMin, restMax);   // arrived — rest, then head somewhere new
            return;
        }

        to.Normalize();
        ctx.controller.Move(new Vector2(to.x, to.z) * amble);
    }

    void PickDest(AIContext ctx)
    {
        // Try a few random spots in range; reject water / off-map so the roam stays on land (the frog spawned
        // on a baked walkable cell, so home is safe). If every pick lands off it — e.g. spawned by a pond edge —
        // just hold at home this round rather than walking into the water.
        for (int i = 0; i < 8; i++)
        {
            Vector2 dir = UnityEngine.Random.insideUnitCircle.normalized;
            if (dir == Vector2.zero) dir = Vector2.up;
            float dist = UnityEngine.Random.Range(0.4f, 1f) * radius;
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
