using UnityEngine;

// The look of the veil across one day, keyed on normalized time t (0 = midnight, 0.5 = noon, 1 = next
// midnight). Feeds the scene's LightManager (URP renders one pass per overlay -> two levers + glare):
//   ambientColor -> LightManager.ambientColor: colour MULTIPLIED into the veil (white day, warm dusk)
//   intensity    -> LightManager.lightIntensity: 1 = bright day, ~0 = dark night
//   fogColor     -> LightManager.fogColor: ADDITIVE light on the Fog overlay = the glare itself (there is
//                   no separate glare lever). rgb = light added, black = off, bright = "chói" (HDR ok).
// Day is longer than night here: sunrise ~06:00, sunset ~20:00 (14h day / 10h night).
[CreateAssetMenu(menuName = "Environment/Day Night")]
public class DayNightConfig : ScriptableObject
{
    [Header("Over the day (t: 0 = midnight, 0.5 = noon)")]
    public Gradient ambientColor;              // -> LightManager.ambientColor (multiply tint)
    public Gradient fogColor;                  // -> LightManager.fogColor (additive light = the glare; rgb, black = off)
    public AnimationCurve intensity =          // -> LightManager.lightIntensity: bright day, ~0 night
        AnimationCurve.Constant(0f, 1f, 1f);

    // Turns an instant into the veil settings it should show. Weather (later) transforms the result.
    public EnvironmentState Evaluate(float t) => new EnvironmentState
    {
        ambientColor = ambientColor.Evaluate(t),
        fogColor = fogColor.Evaluate(t),
        intensity = Mathf.Clamp01(intensity.Evaluate(t)),
    };

    // Fills a believable default palette. Runs when the asset is created (Create > Environment > Day
    // Night) and on the inspector's Reset. Tweak by eye afterwards.
    void Reset()
    {
        // Multiply tint on the veil: white through the day, a strong sunset red at dusk. (The night
        // colour barely matters — intensity drops the veil to near-black anyway.)
        ambientColor = Grad(
            (0.00f, Color.white),
            (0.68f, Color.white),
            (0.75f, new Color(1.00f, 0.80f, 0.62f)),   // sunset begins — soft warm
            (0.80f, new Color(1.00f, 0.68f, 0.48f)),   // peak — warm amber (not blood red)
            (0.86f, new Color(0.92f, 0.72f, 0.66f)),   // fading
            (0.92f, Color.white),
            (1.00f, Color.white));

        // Additive light on the Fog overlay = the glare (black = off): off most of the day, a warm
        // midday glare (#9A9D53) that pushes the scene toward bloom, a faint warm glow at dusk.
        // TODO(weather): the midday glare really belongs to a "sunny" weather (overcast noon wouldn't
        // glare) — move it into SunnyWeather at the seam later.
        fogColor = Grad(
            (0.00f, Color.black),                       // night, off
            (0.30f, Color.black),                       // ~07:00, off — starts rising after
            (0.42f, new Color(0.675f, 0.682f, 0.447f)), // ~10:00, full sun (#ACAE72)
            (0.58f, new Color(0.675f, 0.682f, 0.447f)), // ~14:00, full sun — strong 4h midday plateau
            (0.72f, Color.black),                       // ~17:15, faded off
            (0.80f, new Color(0.15f, 0.07f, 0.03f)),    // ~19:10, dusk warm glow
            (1.00f, Color.black));                      // night, off

        // Brightness: near-0 at night (dark; point lights punch through), full 1 through a long day.
        intensity = Smooth(
            new Keyframe(0.00f, 0.03f),   // midnight, dark
            new Keyframe(0.22f, 0.03f),   // ~05:15, night
            new Keyframe(0.28f, 0.50f),   // ~06:40, dawn rising
            new Keyframe(0.34f, 1.00f),   // ~08:10, full day
            new Keyframe(0.74f, 1.00f),   // ~17:45, still day
            new Keyframe(0.80f, 0.55f),   // ~19:10, dusk
            new Keyframe(0.86f, 0.15f),   // ~20:40, near night
            new Keyframe(0.92f, 0.03f),   // ~22:00, night
            new Keyframe(1.00f, 0.03f));  // midnight
    }

    [ContextMenu("Fill Default Palette")]
    void FillDefaultPalette() => Reset();

    static Gradient Grad(params (float t, Color c)[] stops)
    {
        var g = new Gradient();
        var ck = new GradientColorKey[stops.Length];
        var ak = new GradientAlphaKey[stops.Length];
        for (int i = 0; i < stops.Length; i++)
        {
            ck[i] = new GradientColorKey(stops[i].c, stops[i].t);
            ak[i] = new GradientAlphaKey(stops[i].c.a, stops[i].t);
        }
        g.SetKeys(ck, ak);
        return g;
    }

    static AnimationCurve Smooth(params Keyframe[] keys)
    {
        var c = new AnimationCurve(keys);
        for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f);
        return c;
    }
}

// The resolved veil settings for one instant. Weather transforms these (dim intensity, add glare/haze
// via fogColor, tint ambient) before they reach the LightManager.
public struct EnvironmentState
{
    public Color ambientColor;
    public Color fogColor;
    public float intensity;
}
