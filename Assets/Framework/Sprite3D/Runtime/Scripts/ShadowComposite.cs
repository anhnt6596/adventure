using UnityEngine;

// Draws the single "fill" pass for merged ground shadows. Spawns one ground-plane quad that follows the
// camera and, via the GroundShadowFill material, darkens the ground exactly where the shadow quads (using
// the GroundShadowStencil material) marked the stencil — so overlapping shadows merge into one flat region
// instead of stacking. One quad, no render texture, hard-edged: cheap enough for mobile.
//
// Setup: just drop this once in the scene — its presence flips every SpriteShadow into merge mode (they
// auto-switch to the stencil material and sort themselves just below this fill). Order ends up:
// ground → shadow-stencil quads (write) → this fill (read) → billboards (drawn on top).
[DefaultExecutionOrder(-100)]   // claim Instance before any SpriteShadow.Awake reads it
[DisallowMultipleComponent]
public class ShadowComposite : MonoBehaviour
{
    [SerializeField] Material fillMaterial;      // GroundShadowFill material (auto-made from the shader if null)
    [SerializeField] float size = 80f;           // world size of the fill quad — big enough to cover the view
    [SerializeField] float height = 0.03f;       // just above the shadow-stencil quads
    [SerializeField] string sortingLayer = "";   // blank = keep Default; match the sprites' sorting layer
    [SerializeField] int sortingOrder = -1;      // BELOW billboards; the stencil quads sit one below this
    [SerializeField] string layer = "";          // Unity layer the fill renders on — MUST be one the main
                                                 // camera draws + where the shadows write stencil (the world
                                                 // layer). Blank = this GameObject's own layer.

    // Present ⇒ merge mode is on. SpriteShadow reads this to pick the stencil material + its sorting.
    public static ShadowComposite Instance { get; private set; }
    public string SortingLayer => sortingLayer;
    public int SortingOrder => sortingOrder;

    static Material _autoFill;
    static Sprite _unit;

    SpriteRenderer _sr;
    Camera _cam;

    void Awake()
    {
        var mat = fillMaterial != null ? fillMaterial : AutoFill();
        if (mat == null)
        {
            Debug.LogError($"[{nameof(ShadowComposite)}] shader 'Sprite/GroundShadowFill' not found.", this);
            enabled = false;
            return;   // leave Instance null → SpriteShadows stay on the plain colour shadow (safe fallback)
        }

        var go = new GameObject("ShadowFill");
        go.transform.SetParent(transform, false);
        int l = string.IsNullOrEmpty(layer) ? gameObject.layer : LayerMask.NameToLayer(layer);
        go.layer = l >= 0 ? l : gameObject.layer;    // must match the camera that wrote the stencil
        _sr = go.AddComponent<SpriteRenderer>();
        _sr.sprite = Unit();
        _sr.sharedMaterial = mat;
        if (!string.IsNullOrEmpty(sortingLayer)) _sr.sortingLayerName = sortingLayer;
        _sr.sortingOrder = sortingOrder;

        _cam = Camera.main;
        Instance = this;   // only now that the fill actually exists
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void LateUpdate()
    {
        if (_sr == null) return;
        if (_cam == null) _cam = Camera.main;

        // Centre on where the camera LOOKS at the ground, not on the camera itself — a tilted camera sees
        // ground well in front of it, so centring on its position leaves distant shadows outside the quad.
        Vector3 center;
        if (_cam != null)
        {
            Vector3 pos = _cam.transform.position;
            Vector3 fwd = _cam.transform.forward;
            center = Mathf.Abs(fwd.y) > 1e-4f ? pos + fwd * ((height - pos.y) / fwd.y) : pos;
        }
        else center = transform.position;
        center.y = height;

        // Lie flat on the ground (Cull Off, so facing doesn't matter).
        _sr.transform.SetPositionAndRotation(center, Quaternion.Euler(90f, 0f, 0f));
        _sr.transform.localScale = new Vector3(size, size, 1f);
    }

    static Material AutoFill()
    {
        if (_autoFill == null)
        {
            var sh = Shader.Find("Sprite/GroundShadowFill");
            if (sh != null) _autoFill = new Material(sh) { name = "GroundShadowFill (auto)" };
        }
        return _autoFill;
    }

    static Sprite Unit()
    {
        if (_unit == null)
            _unit = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        return _unit;
    }
}
