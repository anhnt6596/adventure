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
    [SerializeField] float sunriseHour = 6f;
    [SerializeField] float sunsetHour = 20f;

    [Header("Shape")]
    [SerializeField] Vector2 dawnDir = new Vector2(1f, -0.35f);   // ground XZ the shadow points at sunrise
    [SerializeField] Vector2 duskDir = new Vector2(-1f, -0.35f);  // ...and at sunset (swings across the day)
    [SerializeField] float maxLength = 2.5f;                       // shear per unit height at low sun
    [SerializeField, Range(0f, 1f)] float noonScale = 0.12f;       // stub length at midday (× maxLength)
    [SerializeField, Range(0f, 1f)] float strength = 0.4f;         // shadow alpha at full day

    [Tooltip("Fraction of the day at each end over which the shadow fades in/out. Only this window " +
             "touches alpha, so the long low-sun shadows stay visible instead of fading with length.")]
    [SerializeField, Range(0.01f, 0.5f)] float twilight = 0.07f;

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

        Vector2 shear = Vector2.zero;
        float alpha = 0f;
        if (isDay)
        {
            // Cosine curve, not a triangle: the minimum at noon is a rounded bottom, so length
            // eases in and out of the stub instead of snapping there and back. The old plateau came
            // from the max() floor, not the curve - lerping to noonScale keeps the smooth minimum
            // without ever pinning the length flat.
            float noonT = Mathf.Sin(Mathf.Clamp01(day) * Mathf.PI);            // 0 at dawn/dusk, 1 at noon
            float length = maxLength * Mathf.Lerp(1f, noonScale, noonT);
            shear = dir * length;

            // Fade is a dawn/dusk window on its own knob, decoupled from length, so the twilight
            // ramp can be tuned without touching how long the low-sun shadows stretch.
            float fade = Mathf.Min(day, 1f - day) / twilight;
            alpha = strength * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(fade));
        }

        Shader.SetGlobalVector(SunDirId, new Vector4(shear.x, shear.y, 0f, 0f));
        Shader.SetGlobalFloat(StrengthId, alpha);
        Shader.SetGlobalFloat(GroundYId, groundY);
    }

    void OnDisable() => Shader.SetGlobalFloat(StrengthId, 0f);   // system off → no shadows linger
}
