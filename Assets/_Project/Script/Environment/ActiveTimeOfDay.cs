using VContainer;

// The clock the world is currently living by. The overworld's is the default; an arena run pushes its own on
// the way in and releases it on the way out.
//
// A DELEGATE, NOT A SWITCH ON THE VIEWS. DayNightLighting and ShadowSun inject ITimeOfDay and never learn
// there is more than one clock. Without this seam every component that draws the sky would grow an "am I in
// an arena" branch — and each one would be free to answer it differently.
//
// RELEASE NAMES THE CLOCK IT IS RELEASING and ignores a stale one. Ending a run that has already handed the
// world to something else must not drag the sky back to the overworld's hour.
public class ActiveTimeOfDay : ITimeOfDay
{
    readonly ITimeOfDay _default;
    ITimeOfDay _override;

    [Inject]
    public ActiveTimeOfDay(DayNightClock overworld) => _default = overworld;

    ITimeOfDay Current => _override ?? _default;

    public float Time01 => Current.Time01;
    public int Day => Current.Day;
    public float Hour => Current.Hour;

    public void Use(ITimeOfDay clock) => _override = clock;

    public void Release(ITimeOfDay clock)
    {
        if (ReferenceEquals(_override, clock)) _override = null;
    }
}
