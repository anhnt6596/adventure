using UnityEngine;
using VContainer;

// Reads the clock and drives the scene's LightManager veil to match the time of day.
// Model (ITimeOfDay) ⊥ view (LightManager): this never advances time, only pushes the current instant.
//
// ITimeOfDay, not DayNightClock: inside an arena the run's own clock is the one in charge, and this must follow
// it without knowing that gates exist. See ActiveTimeOfDay.
[DisallowMultipleComponent]
public class DayNightLighting : MonoBehaviour
{
    [SerializeField] LightManager lightManager;   // the scene's veil/darkness driver — drag it in

    // The loaded map's own palette, or null to use the scene default. A static, exactly like
    // MapBorderFog.Terrain and for the same reason: MapService points the scene's world systems at the map it
    // just built, and there is only ever one of each of them.
    //
    // WRITTEN ON EVERY SWAP, null included — a map with no palette of its own must fall back to the scene's
    // rather than inherit the last place's sky.
    public static DayLightConfig MapPalette;

    ITimeOfDay _clock;
    DayLightConfig _sceneDefault;

    [Inject]
    public void Construct(ITimeOfDay clock, DayLightConfig config)
    {
        _clock = clock;
        _sceneDefault = config;
    }

    void Start()
    {
        if (_sceneDefault == null) Debug.LogError($"[{nameof(DayNightLighting)}] DayLightConfig not injected — assign it on GameScope.", this);
        if (_clock == null) Debug.LogError($"[{nameof(DayNightLighting)}] {nameof(ITimeOfDay)} not injected — add this GameObject to GameScope's Auto Inject list.", this);
        if (lightManager == null) Debug.LogError($"[{nameof(DayNightLighting)}] LightManager not assigned — drag the scene's LightManager in.", this);
    }

    void LateUpdate()
    {
        var config = MapPalette != null ? MapPalette : _sceneDefault;
        if (config == null || _clock == null || lightManager == null) return;

        EnvironmentState env = config.Evaluate(_clock.Time01);

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
