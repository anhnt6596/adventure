using UnityEngine;
using VContainer;

// Reads the clock and drives the scene's LightManager veil to match the time of day.
// Model (DayNightClock) ⊥ view (LightManager): this never advances time, only pushes the current instant.
[DisallowMultipleComponent]
public class DayNightLighting : MonoBehaviour
{
    [SerializeField] LightManager lightManager;   // the scene's veil/darkness driver — drag it in

    DayNightClock _clock;
    DayNightConfig _config;

    [Inject]
    public void Construct(DayNightClock clock, DayNightConfig config)
    {
        _clock = clock;
        _config = config;
    }

    void Start()
    {
        if (_config == null) Debug.LogError($"[{nameof(DayNightLighting)}] DayNightConfig not injected — assign it on GameScope.", this);
        if (_clock == null) Debug.LogError($"[{nameof(DayNightLighting)}] DayNightClock not injected — add this GameObject to GameScope's Auto Inject list.", this);
        if (lightManager == null) Debug.LogError($"[{nameof(DayNightLighting)}] LightManager not assigned — drag the scene's LightManager in.", this);
    }

    void LateUpdate()
    {
        if (_config == null || _clock == null || lightManager == null) return;

        EnvironmentState env = _config.Evaluate(_clock.Time01);

        // --- Weather seam --------------------------------------------------------
        // When weather lands, transform `env` HERE before pushing, so it layers on the
        // day/night base (blend 0->1 as it rolls in/out for smoothness):
        //   env.fogColor    += weather.glare/haze;   // additive: sunny "chói", bright mist
        //   env.ambientColor = Color.Lerp(env.ambientColor, weather.ambient, weather.blend);
        //   env.intensity    = Mathf.Clamp01(env.intensity - weather.darken);   // storm dims the day
        // -------------------------------------------------------------------------

        lightManager.ambientColor = env.ambientColor;
        lightManager.fogColor = env.fogColor;
        lightManager.lightIntensity = env.intensity;
    }
}
