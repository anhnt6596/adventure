using VContainer.Unity;

// Advances the time of day, looping each cycle. The single source of truth for "what time is it" — the
// lighting view reads it, and later so will spawns and weather. Ticked by VContainer's entry-point loop.
public class DayNightClock : ITickable
{
    // Hard-coded for now (config + save come later): 3 real minutes per in-game day, start at 7am.
    const float DayLengthSeconds = 60;
    const float StartTime = 7f / 24f;

    public float Time01 { get; private set; } = StartTime;   // TODO: load from save instead of StartTime
    public int Day { get; private set; }
    public float Hour => Time01 * 24f;

    public void Tick()
    {
        Time01 += UnityEngine.Time.deltaTime / DayLengthSeconds;
        while (Time01 >= 1f) { Time01 -= 1f; Day++; }   // wrap into the next day
    }
}
