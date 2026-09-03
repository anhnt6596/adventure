// What time it is, to whatever is showing it.
//
// TWO CLOCKS ANSWER THIS AND THEY ARE NOT THE SAME KIND OF THING. DayNightClock runs the overworld, where the
// time of day is decoration — nothing out there reads it but the veil and the shadows. RunClock runs inside
// an arena, where it is the rules: vision, spawn tables, conditional buffs and the survive-X-days win
// condition all hang off it.
//
// THE VIEWS MUST NOT KNOW WHICH ONE THEY ARE LOOKING AT. DayNightLighting and ShadowSun ask for this and get
// whichever clock currently holds the world (see ActiveTimeOfDay), so walking into an arena cannot leave the
// screen lit by the hour of somewhere else — and neither of them grows a branch for a mode it doesn't care
// about.
public interface ITimeOfDay
{
    float Time01 { get; }   // 0 = midnight, 0.5 = noon, wraps at 1
    int Day { get; }        // whole days elapsed since this clock started
    float Hour { get; }     // Time01 × 24, for anything that reads better in hours
}
