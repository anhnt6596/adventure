using UnityEngine;

// The clock inside one gate run: starts at the hour the gate opens on and counts the days survived. Born when
// the run starts, gone when it ends.
//
// A DAY IS A FULL CYCLE FROM THE MOMENT YOU WALKED IN, not a calendar day ending at midnight. Credit the
// player at midnight and the first day is a fraction of every other one, and worse, the night — the part the
// run is actually about — falls inside the first day for a gate that opens at dawn and across the boundary
// for one that opens at dusk. Counting cycles makes "survive three days" mean three of the same thing
// whatever hour the gate opens on, which is what an author needs it to mean.
//
// ELAPSED SECONDS ARE THE ONLY STATE; the hour and the day count are arithmetic over them. A stored day
// counter would be a second answer to "how far in are we", free to drift from the time actually spent — the
// same rule PayGateSystem keeps by storing deposits and never a paid flag.
//
// NOT AN ITickable. ArenaRunner ticks it, because ArenaRunner owns the run: a clock that ran itself would keep
// counting through the death screen and the walk back out.
public class RunClock : ITimeOfDay
{
    readonly float _dayLength;
    readonly float _start01;
    readonly int _days;

    float _elapsed;

    public RunClock(int days, float dayLengthSeconds, float startHour)
    {
        _days = Mathf.Max(1, days);
        _dayLength = Mathf.Max(1f, dayLengthSeconds);
        _start01 = Mathf.Repeat(startHour / 24f, 1f);
    }

    public float Time01 => Mathf.Repeat(_start01 + _elapsed / _dayLength, 1f);
    public int Day => Mathf.FloorToInt(_elapsed / _dayLength);
    public float Hour => Time01 * 24f;

    public int DaysToClear => _days;
    public bool Cleared => Day >= _days;

    // 0..1 through the current day, for a HUD that wants to draw the day as a bar rather than a number.
    public float DayProgress => Mathf.Repeat(_elapsed / _dayLength, 1f);

    public void Tick(float deltaTime) => _elapsed += Mathf.Max(0f, deltaTime);
}
