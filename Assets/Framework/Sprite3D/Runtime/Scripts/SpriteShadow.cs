using UnityEngine;

// Ground shadow for a billboard sprite. The shadow is the sprite's silhouette pinned at the caster's
// ground point and stretched along the sun — fully camera-independent: rotating the camera never moves
// or skews it. ShadowSun feeds the sun direction/darkness as global shader values from the time of day.
//
// Algorithm, per caster:
//   1. a shadow SpriteRenderer (mirrors the sprite) is parented to the stable ROOT (not the billboard art)
//   2. it sits at the ground point (root XZ, ground Y), lifted so the sprite's BASE rests on the ground
//   3. it stays upright; only its local Y yaw is driven to the sun (width perpendicular to the sun)
//   4. the GroundShadow shader shears each vertex's height onto the ground along the global sun direction
//
// Put this on the object's GROUND NODE (scaleNote): the node with the Billboard, whose world position is
// where the object stands on the ground. The shadow hangs off its parent, so it stays put and flat while
// the sprite billboards. `source` = the art SpriteRenderer (assign it — a caster may have others).
[DisallowMultipleComponent]
public class SpriteShadow : MonoBehaviour
{
    [SerializeField] SpriteRenderer source;    // the art sprite to cast (auto-found in children if null)
    [SerializeField] Material material;         // GroundShadow material (auto-made from the shader if null)
    [SerializeField] int orderOffset = -1;      // sorting order vs the source (non-merge mode only)
    [SerializeField] Vector2 groundOffset;      // XZ nudge if the trunk isn't exactly over the root
    [SerializeField] float yawOffset;           // fine-tune the sun-facing yaw (degrees)
    [SerializeField] float scale = 1.05f;       // shadow size relative to the art

    static Material _autoColor, _autoStencil;

    SpriteRenderer _shadow;
    Sprite _lastSprite;

    void Awake()
    {
        if (source == null) source = GetComponentInChildren<SpriteRenderer>();
        if (source == null)
        {
            Debug.LogError($"[{nameof(SpriteShadow)}] no source SpriteRenderer found — assign one.", this);
            enabled = false;
            return;
        }

        // Merge mode is on whenever a ShadowComposite is in the scene: shadows write stencil (invisible) and
        // it fills them once, so overlaps don't stack. Otherwise each shadow draws its own colour. The right
        // material is auto-picked unless you assigned one.
        var comp = ShadowComposite.Instance;
        bool merge = comp != null;
        var mat = material != null ? material : (merge ? AutoStencil() : AutoColor());
        if (mat == null)
        {
            Debug.LogError($"[{nameof(SpriteShadow)}] ground shadow shader not found.", this);
            enabled = false;
            return;
        }

        // Convention: this component sits on the object's GROUND NODE (scaleNote) — the node that also
        // carries the Billboard, and whose world position IS the object's spot on the ground. Hang the
        // shadow off that node's PARENT (stable, never rotates) so it never inherits the camera-facing
        // spin, placed at the ground node's own position.
        Transform anchor = transform.parent != null ? transform.parent : transform;

        var go = new GameObject("Shadow");
        go.transform.SetParent(anchor, false);
        go.layer = source.gameObject.layer;          // same layer as the sprite, or the main cam culls it

        // Overlap the art's AUTHORED position — the convention already places the art so its base rests on
        // the ground, so mirroring that position puts the shadow's base there too (no pivot/bounds guess).
        // Captured now, before the billboard tilts the art. localScale matches the art's world size.
        go.transform.localPosition = anchor.InverseTransformPoint(source.transform.position)
                                     + new Vector3(groundOffset.x, 0f, groundOffset.y);
        go.transform.localScale = source.transform.lossyScale * scale;

        _shadow = go.AddComponent<SpriteRenderer>();
        _shadow.sharedMaterial = mat;
        _shadow.sprite = source.sprite;
        if (merge)
        {
            // Sit one below the fill so the stencil is written before the fill reads it.
            if (!string.IsNullOrEmpty(comp.SortingLayer)) _shadow.sortingLayerName = comp.SortingLayer;
            else _shadow.sortingLayerID = source.sortingLayerID;
            _shadow.sortingOrder = comp.SortingOrder - 1;
        }
        else
        {
            _shadow.sortingLayerID = source.sortingLayerID;
            _shadow.sortingOrder = source.sortingOrder + orderOffset;
        }
        _lastSprite = source.sprite;
    }

    // Driven once per frame by ShadowManager (no per-object LateUpdate). `orient` is true only when the
    // sun actually moved, so the yaw write is skipped on still frames — most of the time, for a static tree.
    public void Tick(float sunYaw, bool orient, Vector4 sun, Vector3 camRight)
    {
        if (_shadow == null) return;

        // Sprite for animation (ref compare, ~free when static).
        if (source.sprite != _lastSprite) { _shadow.sprite = source.sprite; _lastSprite = source.sprite; }

        // Upright, yaw to the sun (width perpendicular to it) — same for every shadow, so only on change.
        if (orient) _shadow.transform.localRotation = Quaternion.Euler(0f, sunYaw + yawOffset, 0f);

        // Flip = sprite's own flipX  ^  the caster's facing (scaleNote.scale.x, which the shadow doesn't
        // inherit)  ^  the billboard-shadow mirror: a camera-facing cutout's shadow flips left/right when
        // the camera crosses to the far side of the sun. shadow-right (world) = (sunZ, -sunX) = (sun.y, -sun.x).
        bool camMirror = camRight.x * sun.y - camRight.z * sun.x < 0f;
        bool flip = source.flipX ^ (transform.localScale.x < 0f) ^ camMirror;
        if (_shadow.flipX != flip) _shadow.flipX = flip;
    }

    // Register with the manager and toggle with the caster (pooled objects disabled in the pool don't cast).
    void OnEnable()
    {
        if (_shadow == null) return;
        _shadow.enabled = true;
        ShadowManager.Register(this);
    }

    void OnDisable()
    {
        if (_shadow != null) _shadow.enabled = false;
        ShadowManager.Unregister(this);
    }

    static Material AutoColor()   => Auto("Sprite/GroundShadow",        ref _autoColor);
    static Material AutoStencil() => Auto("Sprite/GroundShadowStencil", ref _autoStencil);

    static Material Auto(string shaderName, ref Material cache)
    {
        if (cache == null)
        {
            var sh = Shader.Find(shaderName);
            if (sh != null) cache = new Material(sh) { name = shaderName + " (auto)" };
        }
        return cache;
    }
}
