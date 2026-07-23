using UnityEngine;

// Fake water reflection for a billboard sprite — the cheap 2D trick, not a rendered mirror. A second
// SpriteRenderer mirrors the art, faces the camera exactly like it, and hangs flipped below the caster's
// feet. The Sprite/WaterReflection shader tints it to the water, ripples it, and clips it to the water
// surface via the stencil the water shader writes — so it only ever shows on water, never on land.
//
// Put this on the object's GROUND NODE (scaleNode): the node with the Billboard, whose world position is
// where the object stands. The reflection hangs off that node's stable PARENT so it never inherits the
// camera-facing spin; ReflectionManager reorients and repositions it each frame. `source` = the art
// SpriteRenderer (assign it — same one SpriteShadow uses).
[DisallowMultipleComponent]
public class WaterReflection : MonoBehaviour
{
    [SerializeField] SpriteRenderer source;    // the art sprite to reflect (auto-found in children if null)
    [SerializeField] Material material;         // WaterReflection material (auto-made from the shader if null)
    [SerializeField] int orderOffset = -10;      // under the ground layers (so the shore overhang hides its
                                                // edge) but over the water — tune per your terrain sorting
    [SerializeField] float surfaceOffset;       // nudge the reflection up/down along world Y (waterline tweak)
    [SerializeField] float scale = 1f;          // overall size vs the caster (1 = same width as the sprite)
    [SerializeField, Range(0.2f, 1f)] float length = 0.7f;  // foreshorten: a real reflection is shorter than
                                                            // the object; squashes height, feet stay pinned

    static Material _auto;

    SpriteRenderer _reflection;
    Sprite _lastSprite;

    void Awake()
    {
        if (source == null) source = GetComponentInChildren<SpriteRenderer>();
        if (source == null)
        {
            Debug.LogError($"[{nameof(WaterReflection)}] no source SpriteRenderer found — assign one.", this);
            enabled = false;
            return;
        }

        var mat = material != null ? material : Auto();
        if (mat == null)
        {
            Debug.LogError($"[{nameof(WaterReflection)}] '{ShaderName}' shader not found.", this);
            enabled = false;
            return;
        }

        // Hang the reflection off the ground node's PARENT (stable, never billboards) so the mirror we
        // apply here isn't fought by the node's camera-facing spin.
        Transform anchor = transform.parent != null ? transform.parent : transform;

        var go = new GameObject("Reflection");
        go.transform.SetParent(anchor, false);
        go.layer = source.gameObject.layer;          // same layer as the sprite, or the main cam culls it

        _reflection = go.AddComponent<SpriteRenderer>();
        _reflection.sharedMaterial = mat;
        _reflection.sprite = source.sprite;
        _reflection.flipY = true;                    // the vertical mirror; ReflectionManager does the rest
        _reflection.sortingLayerID = source.sortingLayerID;
        _reflection.sortingOrder = source.sortingOrder + orderOffset;
        _lastSprite = source.sprite;
    }

    // Driven once per frame by ReflectionManager. The reflection tracks the caster every frame (it moves,
    // it animates), so unlike the shadow's sun-yaw there's nothing to skip.
    public void Tick(Vector3 camForward)
    {
        if (_reflection == null) return;

        if (source.sprite != _lastSprite) { _reflection.sprite = source.sprite; _lastSprite = source.sprite; }

        var tr = _reflection.transform;
        tr.forward = camForward;                     // face the camera exactly like the art billboard

        // Match the caster's world size, then foreshorten the height so the reflection reads shorter than
        // the object (as a real one does). Feet stay on the waterline because the top-align below runs after.
        // Use the magnitude: lossyScale carries the caster's facing flip (scaleNode.x = -1), and letting that
        // sign into localScale would mirror the quad a second time on top of flipX and cancel the facing out.
        Vector3 ls = source.transform.lossyScale;
        tr.localScale = new Vector3(Mathf.Abs(ls.x) * scale, Mathf.Abs(ls.y) * scale * length, Mathf.Abs(ls.z) * scale);

        // The caster's visual feet: bounds is its world AABB, so min.y is the feet and center.xz the
        // trunk — both pivot-independent. Place the mirror there, then read its OWN bounds and slide it so
        // its top edge lands exactly on the feet: the reflection hangs straight down whatever the sprite's
        // pivot or flip, no half-height guesswork.
        var b = source.bounds;
        Vector3 feet = new Vector3(b.center.x, b.min.y + surfaceOffset, b.center.z);
        tr.position = feet;
        var rb = _reflection.bounds;
        tr.position += new Vector3(feet.x - rb.center.x, feet.y - rb.max.y, feet.z - rb.center.z);

        // Match the caster's horizontal facing (its own flipX xor the ground node's flip, which the
        // reflection doesn't inherit sitting under the parent).
        bool flip = source.flipX ^ (transform.localScale.x < 0f);
        if (_reflection.flipX != flip) _reflection.flipX = flip;
    }

    void OnEnable()
    {
        if (_reflection == null) return;
        _reflection.enabled = true;
        ReflectionManager.Register(this);
    }

    void OnDisable()
    {
        if (_reflection != null) _reflection.enabled = false;
        ReflectionManager.Unregister(this);
    }

    const string ShaderName = "Sprite/WaterReflection";

    static Material Auto()
    {
        if (_auto == null)
        {
            var sh = Shader.Find(ShaderName);
            if (sh != null) _auto = new Material(sh) { name = ShaderName + " (auto)" };
        }
        return _auto;
    }
}
