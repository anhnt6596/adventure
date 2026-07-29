using UnityEngine;

// Put this on anything that must get out of the way when it stands between the camera and the player — a tree,
// a rock, a wall prop. It fades its sprite down while it covers the player and back up when it doesn't.
//
// The fade is NOT a flat alpha: the base stays solid and only the upper part thins out, so a tree that gets out
// of the way still reads as a tree standing there rather than a ghost. Sprite/Flash does the ramp; this pushes
// the two heights and the target alpha into it per renderer.
//
// It shares a MaterialPropertyBlock with HitFlash, which is why HitFlash zeroes its flash instead of dropping
// the block — see the note there. Both read correctly at once: a tree can be mid-flash and mid-fade.
//
// The test itself lives here (so the knobs are per-object) but the shared camera maths is done once for
// everyone in BlockerFadeManager.
[DisallowMultipleComponent]
public class FadeWhenBlocking : MonoBehaviour
{
    static readonly int FadeAlphaId = Shader.PropertyToID("_FadeAlpha");
    static readonly int FadeY0Id = Shader.PropertyToID("_FadeY0");
    static readonly int FadeY1Id = Shader.PropertyToID("_FadeY1");

    // Defaults are the values tuned on basic_tree against the real camera, so a new blocker is usable the moment
    // it is dropped on. They are a starting point, not a rule — anything shaped very differently to a tree will
    // want its own pass.
    [SerializeField, Range(0f, 1f)] float fadedAlpha = 0.45f;
    [SerializeField] float fadeSpeed = 6f;   // alpha per second — how fast it clears out and comes back

    // Where the thinning happens, as a fraction of the sprite's own height measured from its BOTTOM. Below
    // `fadeFrom` nothing changes at all — the trunk stays fully solid, so the tree keeps its footing in the
    // scene and you can still tell something is there. Between the two it ramps to `fadedAlpha`, and above
    // `fadeTo` it holds there. Percentages rather than metres, so one setting fits a sapling and an oak.
    [SerializeField, Range(0f, 1f)] float fadeFrom = 0.2f;
    [SerializeField, Range(0f, 1f)] float fadeTo = 0.3f;

    // How much bigger (or smaller) than its own silhouette the "in the way" region is, per EDGE, as a fraction
    // of its screen size. A fraction rather than pixels, so a big tree and a small bush both adjust by the right
    // amount without being tuned apart.
    //
    // Two knobs and not one because the rect is an AABB while the art is not: a canopy tapers, so the top of the
    // box is mostly empty sky and wants pulling IN (negative), while the sides sit close to real pixels and
    // usually want a little slack. One shared number forces a compromise that is wrong at both ends.
    //
    // There is deliberately no bottom knob. The base of the box already hugs the trunk, and the depth test —
    // this thing has to be between the camera and the player at all — is what keeps the low edge honest.
    [SerializeField, Range(-0.9f, 1.5f)] float reach = 0.1f;        // left + right
    [SerializeField, Range(-0.9f, 1.5f)] float reachTop = -0.4f;    // the crown — a tapering canopy needs a lot pulled off

    // Extra reach that applies ONLY while already faded, so the edge you leave by sits further out than the edge
    // you entered by. Without the gap, walking the rim toggles the fade every other frame.
    [SerializeField, Range(0f, 0.5f)] float stickyEdge = 0.06f;

    // Left empty = the billboarded art. NOT the ground shadow — see BlockerFadeManager.ArtRenderersOf. A faded
    // tree keeps its shadow solid, which is what you want: the shadow is on the ground, it was never in the way.
    [SerializeField] SpriteRenderer[] renderers;

    MaterialPropertyBlock _mpb;
    Sprite[] _rangeFor;      // which sprite each renderer's pushed range was computed from
    float _alpha = 1f;
    bool _blocking;

    // Solid and not currently claiming to block — nothing left to do, so the manager can stop carrying it.
    internal bool Settled => _alpha == 1f && !_blocking;

    void Awake()
    {
        if (renderers == null || renderers.Length == 0)
            renderers = BlockerFadeManager.ArtRenderersOf(gameObject);

        _mpb = new MaterialPropertyBlock();
        _rangeFor = new Sprite[renderers.Length];
    }

    void OnEnable() => BlockerFadeManager.Register(this);

#if UNITY_EDITOR
    // The ramp is cached per sprite, so dragging the sliders in play mode would otherwise do nothing until the
    // sprite changed — which for a tree is never. Forget the cache and let the next write recompute.
    void OnValidate()
    {
        if (_rangeFor != null) System.Array.Clear(_rangeFor, 0, _rangeFor.Length);
    }
#endif

    void OnDisable()
    {
        BlockerFadeManager.Unregister(this);
        _blocking = false;
        _alpha = 1f;
        Apply();   // a pooled object must not come back still faded from its last life
    }

    // No camera or no subject this frame — ease back to solid rather than freezing at whatever alpha we held.
    internal void Clear(float dt)
    {
        _blocking = false;
        if (_alpha == 1f) return;
        _alpha = Mathf.MoveTowards(_alpha, 1f, fadeSpeed * dt);
        Apply();
    }

    // Called once per frame by the manager, with the camera maths already done.
    internal void Tick(Camera cam, Vector3 camPos, Vector3 rayDir, float subjectDist, Rect subjectRect, float dt)
    {
        _blocking = IsBlocking(cam, camPos, rayDir, subjectDist, subjectRect);

        float target = _blocking ? fadedAlpha : 1f;
        if (_alpha == target) return;   // settled — stop writing, so a still scene costs nothing at all

        _alpha = Mathf.MoveTowards(_alpha, target, fadeSpeed * dt);
        Apply();
    }

    // Three tests, cheapest first, each one killing most of what reaches it.
    bool IsBlocking(Camera cam, Vector3 camPos, Vector3 rayDir, float subjectDist, Rect subjectRect)
    {
        // Everything below measures from the middle of the SPRITE, never from transform.position. The transform
        // sits on the ground at the trunk; the art stands metres above it and leans toward the camera. Measure
        // from the feet and a tree whose CROWN is swallowing the player is scored as far off the ray — the
        // taller the tree, the worse, which is why the crown was the part that refused to fade.
        var b = BlockerFadeManager.WorldBoundsOf(renderers);
        if (b.extents == Vector3.zero) return false;
        Vector3 centre = b.center;

        // 1. DEPTH. Only something drawn OVER the player can hide them. Comparing sprite centres is also exactly
        //    how Unity sorts these transparent renderers against each other, so this test agrees with the draw
        //    order it is trying to predict instead of approximating it from the ground positions.
        Vector3 toMe = centre - camPos;
        float depth = Vector3.Dot(toMe, rayDir);
        if (depth <= 0f || depth >= subjectDist) return false;

        // 2. THE RAY. Perpendicular distance against the sprite's own half-diagonal — a rotation-INVARIANT
        //    radius, unlike the world AABB, whose size swings with camera yaw and made this quietly reject more
        //    on axis-aligned views than on diagonal ones. Grown by the same slack as the rect below, or a
        //    generous `reach` would be capped here without ever showing up.
        float radius = BlockerFadeManager.WorldRadiusOf(renderers);
        if (radius <= 0f) return false;

        Vector3 perp = toMe - rayDir * depth;
        float widest = Mathf.Max(0f, Mathf.Max(reach, reachTop));
        float allow = radius * (1f + widest + stickyEdge);
        if (perp.sqrMagnitude > allow * allow) return false;

        // 3. THE SHAPE. What survives is genuinely near the line, so now pay for the exact silhouette.
        if (!BlockerFadeManager.ScreenRectOf(cam, renderers, out Rect rect)) return false;

        // Sticky is added to every edge alike: it is anti-flicker, not shaping. Its whole job is that the line
        // you leave by sits outside the line you came in by, whichever way you walked.
        float sticky = _blocking ? stickyEdge : 0f;
        rect = Rect.MinMaxRect(
            rect.xMin - rect.width * (reach + sticky),
            rect.yMin - rect.height * sticky,
            rect.xMax + rect.width * (reach + sticky),
            rect.yMax + rect.height * (reachTop + sticky));

        // The two axes get DIFFERENT questions, because the player's shape on screen is not symmetric about the
        // thing we can cheaply name. Horizontally they are a narrow column, so their centre line is a fair
        // stand-in and treating them as a wide box made trees fade for merely brushing a shoulder. Vertically
        // they are a tall band standing UP from a point the camera keeps pinned at screen centre, so a single
        // height can only ever catch blockers covering that one height — everything covering the head or the
        // legs reads as clear. Point across, whole body up.
        float x = subjectRect.center.x;
        if (x < rect.xMin || x > rect.xMax) return false;
        return rect.yMax >= subjectRect.yMin && rect.yMin <= subjectRect.yMax;
    }

    void Apply()
    {
        if (renderers == null) return;

        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;

            r.GetPropertyBlock(_mpb);          // read-modify-write: HitFlash keeps its values in the same block
            _mpb.SetFloat(FadeAlphaId, _alpha);
            PushFadeRange(r, i);
            r.SetPropertyBlock(_mpb);
        }
    }

    // Turn the authored percentages into the object-space heights the shader ramps between. Done here rather
    // than in the shader because a sprite's UVs are a sub-rect of its sheet, so there is no 0..1 "up the sprite"
    // coordinate available in there — but the mesh IS built from these bounds, so object space has one.
    //
    // Only recomputed when the sprite changes, which for a tree is never and for an animated caster is once per
    // frame at worst.
    void PushFadeRange(SpriteRenderer r, int index)
    {
        var sprite = r.sprite;
        if (sprite == null || _rangeFor[index] == sprite) return;
        _rangeFor[index] = sprite;

        Bounds local = sprite.bounds;
        float bottom = local.center.y - local.extents.y;
        float height = local.extents.y * 2f;

        float from = Mathf.Min(fadeFrom, fadeTo);   // crossed sliders shouldn't invert the ramp
        float to = Mathf.Max(fadeFrom, fadeTo);
        _mpb.SetFloat(FadeY0Id, bottom + height * from);
        _mpb.SetFloat(FadeY1Id, bottom + height * to);
    }
}
