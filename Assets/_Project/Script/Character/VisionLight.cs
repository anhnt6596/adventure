using UnityEngine;
using VContainer;

// The circle of light the character carries at night, sized by the Vision stat.
//
// THE AUTHORED SCALE IS THE BASELINE, captured in Awake before anything can have touched it, and the stat is
// a multiple of it — 1 draws the light exactly as the prefab does. That is the only honest reading of this
// art: the glow FADES OUT, so how far it "reaches" is not a distance anybody can measure off the sprite, it
// is a judgement somebody already made by eye when they set the scale. Deriving a radius from the sprite's
// bounds would size the light by the extent of a texture that is mostly transparent, and putting a distance
// in the config would be that same judgement written down a second time, free to disagree with the art the
// day the glow is redrawn.
//
// Same reason SoulFire caches its authored glow scale in Awake rather than reading it back later: once
// something has scaled the object, the authored value is gone and cannot be recovered.
//
// Vision is NOT pickup radius. Docs/DESIGN.md is explicit that the two must never merge — one decides how
// much of the night you are shown, the other how far loot comes to you.
[DisallowMultipleComponent]
public class VisionLight : MonoBehaviour
{
    Vector3 _authored;   // what the prefab was drawn at; every value below is a multiple of this
    IStat _vision;

    [Inject]
    public void Construct(ICharacterStats stats) => _vision = stats?.Vision;

    void Awake() => _authored = transform.localScale;

    // Start, not OnEnable: injection runs after Awake, so the stat does not exist yet while a freshly
    // instantiated body is enabling its children. Same reason Damageable reads its HP here.
    void Start()
    {
        if (_vision == null)
        {
            Debug.LogError($"[{nameof(VisionLight)}] no {nameof(ICharacterStats)} injected — this belongs on a " +
                           "body PlayerSystem spawns, and does nothing anywhere else.", this);
            return;
        }

        _vision.Changed += Apply;
        Apply();
    }

    // Stats outlive a body, so the listener has to come off with it — a character switch throws this light
    // away while the old set can still move.
    void OnDestroy()
    {
        if (_vision != null) _vision.Changed -= Apply;
    }

    // Clamped at zero rather than allowed to go negative: enough debuffs can take a multiplier below zero
    // (see StatModifier), and a negative scale mirrors the sprite instead of putting the light out.
    void Apply()
    {
        if (_vision == null) return;
        transform.localScale = _authored * Mathf.Max(0f, _vision.Value);
    }
}
