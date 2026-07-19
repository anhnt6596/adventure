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
    [SerializeField] int orderOffset = -1;      // sorting order vs the source, so the shadow sits under it
    [SerializeField] Vector2 groundOffset;      // XZ nudge if the trunk isn't exactly over the root
    [SerializeField] float yawOffset;           // fine-tune the sun-facing yaw (degrees)

    static Material _autoMaterial;
    static readonly int SunDirId = Shader.PropertyToID("_SunGroundDir");   // set globally by ShadowSun

    SpriteRenderer _shadow;
    Sprite _lastSprite;
    Camera _cam;

    void Awake()
    {
        if (source == null) source = GetComponentInChildren<SpriteRenderer>();
        if (source == null)
        {
            Debug.LogError($"[{nameof(SpriteShadow)}] no source SpriteRenderer found — assign one.", this);
            enabled = false;
            return;
        }

        var mat = material != null ? material : AutoMaterial();
        if (mat == null)
        {
            Debug.LogError($"[{nameof(SpriteShadow)}] shader 'Sprite/GroundShadow' not found.", this);
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
        go.transform.localScale = source.transform.lossyScale;

        _shadow = go.AddComponent<SpriteRenderer>();
        _shadow.sharedMaterial = mat;
        _shadow.sprite = source.sprite;
        _shadow.sortingLayerID = source.sortingLayerID;
        _shadow.sortingOrder = source.sortingOrder + orderOffset;
        _lastSprite = source.sprite;
    }

    void LateUpdate()
    {
        if (_shadow == null) return;

        if (source.sprite != _lastSprite) { _shadow.sprite = source.sprite; _lastSprite = source.sprite; }

        // Upright, yaw to the sun (width perpendicular to it). Parent (root) never rotates, so this is
        // camera-independent and X/Z stay 0.
        Vector4 d = Shader.GetGlobalVector(SunDirId);   // _SunGroundDir.xy = world (X,Z) the shadow points
        float sunYaw = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
        _shadow.transform.localRotation = Quaternion.Euler(0f, sunYaw + yawOffset, 0f);

        // Flip = sprite's own flipX  ^  the caster's facing (scaleNote.scale.x, which the shadow doesn't
        // inherit)  ^  the billboard-shadow mirror: a camera-facing cutout's shadow flips left/right when
        // the camera crosses to the far side of the sun. Compare camera-right against the shadow's right.
        if (_cam == null) _cam = Camera.main;
        bool camMirror = false;
        if (_cam != null)
        {
            Vector3 cr = _cam.transform.right;          // shadow right (world) = (sunZ, -sunX) = (d.y, -d.x)
            camMirror = cr.x * d.y - cr.z * d.x < 0f;
        }
        _shadow.flipX = source.flipX ^ (transform.localScale.x < 0f) ^ camMirror;
    }

    // Toggle with the caster so pooled objects (disabled in the pool) don't cast.
    void OnEnable()  { if (_shadow != null) _shadow.enabled = true; }
    void OnDisable() { if (_shadow != null) _shadow.enabled = false; }

    static Material AutoMaterial()
    {
        if (_autoMaterial == null)
        {
            var sh = Shader.Find("Sprite/GroundShadow");
            if (sh != null) _autoMaterial = new Material(sh) { name = "GroundShadow (auto)" };
        }
        return _autoMaterial;
    }
}
