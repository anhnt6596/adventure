using System;
using VContainer.Unity;

// The game's heartbeat: one beat a second, for everything whose numbers are written per second.
//
// WHY A SERVICE AND NOT AN ACCUMULATOR PER CLASS. Hunger already ticked once a second by counting deltaTime
// itself, and every system that joins it — a poison stack, a crop growing, a spawner budget — would copy the
// same six lines and the same catch-up loop. Copies drift: one of them uses unscaled time, one forgets the
// while-loop and swallows a stalled second, and none of them beat on the same frame as the others.
//
// AN EVENT, NOT A PUBLIC Action FIELD. A field would let any subscriber assign over it and silently drop every
// other listener, and would let anything invoke it — a fake second is then one typo away. An event can only be
// subscribed to and unsubscribed from out here, and only fired in Tick. Same choice Hunger.Changed and
// Damageable.Died already made.
//
// SCALED TIME on purpose: Time.deltaTime, so a paused or slowed game pauses the stomach with it. Anything that
// must keep counting through a pause (a real-world timer, an idle-earnings clock) wants wall time and does not
// belong on this — it would be a second event here rather than a flag on this one.
public class TimeService : ITickable
{
    // One second, the unit the configs are authored in. Not a knob — see Hunger's TickInterval, which this
    // replaces.
    const float TickInterval = 1f;

    float _accumulated;

    // Seconds since the app started, counted by beats rather than by asking the clock — so it can never
    // disagree with the number of times NewSecond has fired. For "every 5 seconds" subscribers, which test
    // this instead of keeping a counter of their own.
    public int Seconds { get; private set; }

    public event Action NewSecond;

    public void Tick()
    {
        _accumulated += UnityEngine.Time.deltaTime;
        if (_accumulated < TickInterval) return;

        // A loop, not an if: a stalled frame spanned real seconds and must pay all of them, or a lag spike is
        // free food. Time.maximumDeltaTime is clamped in App.Awake, so in practice this runs once — the loop is
        // for the frame where it does not.
        while (_accumulated >= TickInterval)
        {
            _accumulated -= TickInterval;
            Seconds++;
            NewSecond?.Invoke();
        }
    }
}
