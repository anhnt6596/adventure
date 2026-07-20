using UnityEngine;
using VContainer;

// Drives every GroundShadow material from one place: turns the time of day into a sun direction and
// pushes it as global shader properties. Model (DayNightClock) ⊥ view (the shadow shader): this only
// reads the clock. Shadows swing from one side at dawn, shrink to a stub at noon, stretch the other
// way at dusk, and fade out at night.
//
// Wiring: drop on a scene object and add it to GameScope's Auto Inject list (needs DayNightClock).
// Not injected? It still runs off `fixedHour` so you can test the shader before wiring DI.
[DefaultExecutionOrder(-50)]   // push the sun globals before SpriteShadow reads them (frame-1 correctness)
[DisallowMultipleComponent]
public class ShadowSun : MonoBehaviour
{
    [Header("Day window (match DayNightConfig)")]
    [SerializeField] float sunriseHour = 4f;
    [SerializeField] float sunsetHour = 20f;

    [Header("Shape")]
    [SerializeField] Vector2 dawnDir = new Vector2(-2f, 0.5f);    // ground XZ the shadow points at sunrise
    [SerializeField] Vector2 duskDir = new Vector2(2f, 0.5f);     // ...and at sunset (swings across the day)
    [SerializeField] float maxLength = 1.5f;                       // shear per unit height at low sun
    [SerializeField, Range(0f, 1f)] float noonScale = 0.45f;       // stub length at midday (× maxLength)
    [SerializeField, Range(0f, 1f)] float strength = 0.5f;         // shadow alpha at full day

    [Tooltip("Fraction of the day at each end over which the shadow fades in/out. Only this window " +
             "touches alpha, so the long low-sun shadows stay visible instead of fading with length.")]
    [SerializeField, Range(0.01f, 0.5f)] float twilight = 0.25f;

    [Header("Night")]
    [SerializeField] Vector2 nightDir = new Vector2(0f, 1f);          // fixed ground direction at night (moon)
    [SerializeField] float nightLength = 1.2f;                        // fixed shear length at night
    [SerializeField, Range(0f, 1f)] float nightStrength = 0.4f;       // shadow alpha at night (0 = no night shadow)

    [Tooltip("Hours over which the night shadow fades in after sunset and out before sunrise.")]
    [SerializeField] float nightFade = 0.25f;   // 15 minutes

    [Header("World")]
    [SerializeField] float groundY = 0f;   // world Y of the flat ground the shadows lie on

    [Header("Fallback when no DayNightClock is injected")]
    [SerializeField] float fixedHour = 9f;

    static readonly int SunDirId = Shader.PropertyToID("_SunGroundDir");
    static readonly int StrengthId = Shader.PropertyToID("_ShadowStrength");
    static readonly int GroundYId = Shader.PropertyToID("_ShadowGroundY");

    DayNightClock _clock;

    [Inject]
    public void Construct(DayNightClock clock) => _clock = clock;

    void LateUpdate()
    {
        float hour = _clock != null ? _clock.Hour : fixedHour;

        float day = Mathf.InverseLerp(sunriseHour, sunsetHour, hour);   // 0..1 across the daylit window
        bool isDay = hour > sunriseHour && hour < sunsetHour;

        // Sweep the direction by ANGLE, so it rotates evenly instead of whipping through the middle.
        float dawnYaw = Mathf.Atan2(dawnDir.y, dawnDir.x);
        float duskYaw = Mathf.Atan2(duskDir.y, duskDir.x);
        float yaw = Mathf.LerpAngle(dawnYaw * Mathf.Rad2Deg, duskYaw * Mathf.Rad2Deg, Mathf.Clamp01(day)) * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(yaw), Mathf.Sin(yaw));

        Vector2 shear;
        float alpha;
        if (isDay)
        {
            // Cosine curve, not a triangle: the minimum at noon is a rounded bottom, so length
            // eases in and out of the stub instead of snapping there and back. Lerping to noonScale
            // keeps that smooth minimum without the old max() floor pinning the length flat.
            float noonT = Mathf.Sin(Mathf.Clamp01(day) * Mathf.PI);            // 0 at dawn/dusk, 1 at noon
            shear = dir * (maxLength * Mathf.Lerp(1f, noonScale, noonT));

            float fade = Mathf.Min(day, 1f - day) / twilight;
            alpha = strength * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(fade));
        }
        else
        {
            // Fixed direction and length; only alpha eases, over a short window after sunset and
            // before sunrise, so the night shadow fades in and out at the day boundary.
            shear = nightDir.normalized * nightLength;

            float sinceSunset = Mathf.Repeat(hour - sunsetHour, 24f);   // hours into the night
            float untilSunrise = Mathf.Repeat(sunriseHour - hour, 24f); // hours left of the night
            float edge = Mathf.Min(sinceSunset, untilSunrise) / Mathf.Max(0.001f, nightFade);
            alpha = nightStrength * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge));
        }

        Shader.SetGlobalVector(SunDirId, new Vector4(shear.x, shear.y, 0f, 0f));
        Shader.SetGlobalFloat(StrengthId, alpha);
        Shader.SetGlobalFloat(GroundYId, groundY);
    }

    void OnDisable() => Shader.SetGlobalFloat(StrengthId, 0f);   // system off → no shadows linger
}
