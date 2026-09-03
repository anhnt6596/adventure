using UnityEngine;

// Drifts a light around on the ground plane so a flame does not sit dead still. Put it on the light sprite
// itself (the `spotlight` child of a torch), not on the prop: the art stays where it was planted and only the
// glow wanders.
//
// XZ ONLY. Height is left alone on purpose — the lighting is a flat buffer rendered from above (see
// LightManager), so moving a light up or down changes nothing anybody can see and would only push the sprite
// out of the plane its billboard was drawn for.
//
// PERLIN, NOT A SINE. A sine is a loop, and a loop on a torch is a metronome the eye finds within about two
// seconds — worse than not moving at all, because now it looks mechanical rather than still. Two independent
// noise lanes give a wander with no beat to catch.
//
// THE AUTHORED POSITION IS THE CENTRE, captured in Awake before anything can have moved it. Same rule
// VisionLight keeps for its authored scale: once something has written over the value the prefab was drawn
// with, it cannot be recovered, so it is read once at the start and everything after is an offset from it.
[DisallowMultipleComponent]
public class LightSway : MonoBehaviour
{
    [Tooltip("How far the glow wanders from where it was placed, in world units. Small: this is a flame " +
             "breathing, not a lantern swinging on a rope.")]
    [SerializeField, Min(0f)] float radius = 0.15f;

    [Tooltip("How fast it wanders. Roughly the number of drifts a second — under 1 reads as a flame, above " +
             "that as a wobble.")]
    [SerializeField, Min(0f)] float speed = 0.6f;

    Vector3 _anchor;
    float _phaseX, _phaseZ;

    void Awake()
    {
        _anchor = transform.localPosition;

        // A PHASE PER INSTANCE, or every torch on the map breathes in unison and the whole point is lost.
        // Seeded off the instance id rather than off Random.value so a scene reloaded twice looks the same
        // twice — a light that drifts differently each run is a nuisance to tune against.
        var rng = new System.Random(GetInstanceID());
        _phaseX = (float)rng.NextDouble() * 100f;
        _phaseZ = (float)rng.NextDouble() * 100f;
    }

    // LateUpdate, so this lands after anything that moved the light's parent this frame and before the light
    // camera draws — the same slot the billboards run in.
    void LateUpdate()
    {
        if (radius <= 0f) return;

        float t = Time.time * speed;

        // Two lanes far enough apart in the noise field to be independent. Centred on zero: Perlin returns
        // 0..1, so the wander would otherwise be a drift to one corner rather than a sway around the anchor.
        float x = Mathf.PerlinNoise(_phaseX + t, 0.37f) - 0.5f;
        float z = Mathf.PerlinNoise(0.71f, _phaseZ + t) - 0.5f;

        transform.localPosition = _anchor + new Vector3(x, 0f, z) * (radius * 2f);
    }

#if UNITY_EDITOR
    // The area the glow can reach, drawn on the ground so the radius can be set by eye against the prop it
    // belongs to instead of by playing and squinting.
    void OnDrawGizmosSelected()
    {
        Vector3 centre = Application.isPlaying ? transform.parent != null
                             ? transform.parent.TransformPoint(_anchor) : _anchor
                         : transform.position;

        Gizmos.color = new Color(1f, 0.85f, 0.35f, 0.8f);
        const int seg = 24;
        Vector3 prev = centre + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg;
            Vector3 next = centre + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
#endif
}
