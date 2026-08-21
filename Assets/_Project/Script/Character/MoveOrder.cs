using UnityEngine;

// A place the player asked to walk to, and the steering that gets there. STRAIGHT AT IT — there is no path,
// no avoidance, nothing routed around: the body heads for the point and whatever it meets on the way is left
// to collision. That is the whole design, not a stage on the way to a pathfinder.
//
// WHICH IS WHY GIVING UP IS PART OF IT. A click behind a wall or across water is not refused when it is made
// — the walk toward it is the interesting half, the body slides along the bank and ends up where it can
// plausibly get to — but something has to notice that the walk has stopped going anywhere, or the character
// leans into a rock for the rest of the session. Progress toward the point is the test; see Step.
//
// It holds no reference to the unit: it is handed a position and a speed and returns a steering vector, so
// the same order serves whatever ends up issuing one.
public class MoveOrder
{
    // Close enough to call it arrived. Small on purpose — Step already shortens the last frame so the body
    // lands ON the point rather than stepping past it, so this does not have to cover a frame's travel and
    // is not the thing that decides whether the character stops short of where you clicked.
    const float Arrive = 0.05f;

    // How long the walk may make no headway before the order is dropped.
    const float StallTime = 0.4f;

    // What counts as headway, as a share of the ground a full-speed frame would cover. Not zero: a body
    // grinding along a wall at a shallow angle does creep forward, and an order that survived on that would
    // take a minute to admit it is stuck.
    const float StallProgress = 0.25f;

    Vector3 _dest;
    float _lastDist;
    float _stalled;

    public bool Active { get; private set; }
    public Vector3 Destination => _dest;

    public void Set(Vector3 worldPoint)
    {
        _dest = worldPoint;
        Active = true;
        _lastDist = float.MaxValue;   // no previous frame to compare against — the first one is never a stall
        _stalled = 0f;
    }

    public void Clear()
    {
        Active = false;
        _stalled = 0f;
    }

    // The steering for this frame, in world XZ, magnitude at most 1 — and SHORTER on the last frame, scaled
    // to the ground actually left. Without that the body overshoots the point by up to a frame of travel,
    // turns round, overshoots back, and jitters on the spot for as long as the order lives.
    public Vector2 Step(Vector3 from, float speed, float dt)
    {
        if (!Active) return Vector2.zero;

        Vector3 to = _dest - from; to.y = 0f;
        float dist = to.magnitude;
        if (dist <= Arrive) { Clear(); return Vector2.zero; }

        float full = speed * dt;   // what a frame at full speed covers

        // PROGRESS IS MEASURED TOWARD THE POINT, not as ground covered, and the difference is the whole test:
        // a body sliding along a wall that still brings it closer is getting somewhere and keeps going, while
        // one sliding along a wall that does not is exactly the case this has to give up on. A body with
        // nowhere to go is pushed back where it started every frame, so its distance never falls at all.
        if (_lastDist - dist < full * StallProgress)
        {
            _stalled += dt;
            if (_stalled >= StallTime) { Clear(); return Vector2.zero; }
        }
        else _stalled = 0f;

        _lastDist = dist;

        float scale = full > 0f ? Mathf.Min(1f, dist / full) : 1f;
        return new Vector2(to.x, to.z) * (scale / dist);
    }

    // A frame the order is not being acted on — the unit is mid-swing, or the controls were taken away. It
    // has not stalled, because nothing tried to move: the timer must not run, and the distance has to be
    // re-read or the first frame back counts the gap as a leap forward and hides a real stall behind it.
    public void Hold(Vector3 from)
    {
        if (!Active) return;

        Vector3 to = _dest - from; to.y = 0f;
        _lastDist = to.magnitude;
        _stalled = 0f;
    }
}
