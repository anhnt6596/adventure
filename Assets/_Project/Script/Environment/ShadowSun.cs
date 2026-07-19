using UnityEngine;
using VContainer;

// Drives every GroundShadow material from one place: turns the time of day into a sun direction and
// pushes it as global shader properties. Model (DayNightClock) ⊥ view (the shadow shader): this only
// reads the clock. Shadows swing from one side at dawn, shrink to a stub at noon, stretch the other
// way at dusk, and fade out at night.
//
// Wiring: drop on a scene object and add it to GameScope's Auto Inject list (needs DayNightClock).
// Not injected? It still runs off `fixedHour` so you can test the shader before wiring DI.
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
        float elevation = Mathf.Sin(Mathf.Clamp01(day) * Mathf.PI);      // 0 at edges, 1 at noon

        Vector2 dir = Vector2.Lerp(dawnDir, duskDir, Mathf.Clamp01(day)).normalized;

        Vector2 shear = Vector2.zero;
        float alpha = 0f;
        if (isDay)
        {
            float length = maxLength * Mathf.Max(noonScale, 1f - elevation);   // long low sun, stub at noon
            shear = dir * length;
            alpha = strength * Mathf.SmoothStep(0f, 0.2f, elevation);          // ease in/out near dawn/dusk
        }

        Shader.SetGlobalVector(SunDirId, new Vector4(shear.x, shear.y, 0f, 0f));
        Shader.SetGlobalFloat(StrengthId, alpha);
        Shader.SetGlobalFloat(GroundYId, groundY);
    }

    void OnDisable() => Shader.SetGlobalFloat(StrengthId, 0f);   // system off → no shadows linger
}
