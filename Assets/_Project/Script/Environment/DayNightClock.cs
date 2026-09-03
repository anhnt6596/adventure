using VContainer.Unity;

// Advances the overworld's time of day, looping each cycle. Ticked by VContainer's entry-point loop.
//
// THE OVERWORLD'S CLOCK, NOT THE GAME'S. Out here the time of day is decoration — there are no monsters, no
// vision limit and no spawn table reading it, so nothing breaks if it says a different hour than an arena
// does. Inside an arena the time of day is the rules, and RunClock owns it. Anything DISPLAYING time asks for
// ITimeOfDay and gets whichever is in charge; only a dev tool that means "the overworld clock specifically"
// should ask for this type.
public class DayNightClock : ITimeOfDay, ITickable
{
    // The pace of a day out here. Public because an arena authored to run at the world's pace should say so by
    // pointing at this number rather than by copying it — see ArenaConfig.dayLengthSeconds.
    public const float DefaultDayLengthSeconds = 300f;

    const float StartTime = 7f / 24f;

    public float Time01 { get; private set; } = StartTime;   // TODO: load from save instead of StartTime
    public int Day { get; private set; }
    public float Hour => Time01 * 24f;

    public bool Paused { get; set; }   // freeze the time of day (dev/test; lighting + shadows hold still)

    public void Tick()
    {
        if (Paused) return;
        Time01 += UnityEngine.Time.deltaTime / DefaultDayLengthSeconds;
        while (Time01 >= 1f) { Time01 -= 1f; Day++; }   // wrap into the next day
    }

    // Jump straight to a time of day (0..1, wraps). For scrubbing while paused, or later a save/load.
    public void SetTime01(float t) => Time01 = t - UnityEngine.Mathf.Floor(t);
}
