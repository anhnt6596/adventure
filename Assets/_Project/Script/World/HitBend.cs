using UnityEngine;

// Whips the sprite over when it is struck, and lets it spring back. A tree takes an axe and the canopy lurches
// away from the blow before settling — which is the difference between hitting a tree and hitting a wall.
//
// AWAY FROM WHOEVER SWUNG. The direction is read off the damage source, so a blow from the left throws the
// crown right, and the same tree hit from the other side goes the other way. Reading it from the blow rather
// than picking a side is the whole point: it is the one thing that makes the reaction look caused.
//
// A SHEAR, DONE IN THE SHADER, not a rotation of the transform. These sprites are pivoted at their middle, so
// turning one about its own centre drags the trunk out of the ground it is standing in — see Sprite/Flash Fade,
// which owns the lean. It rides the same MaterialPropertyBlock as HitFlash and FadeWhenBlocking, so a tree can
// be flashing, thinning out and bending all at once.
//
// IN SPRITE-LOCAL UNITS, ACROSS THE SCREEN. The art is billboarded, so its own X is whatever "sideways" means
// to the camera right now — which is exactly the axis a lean should be measured on, and it re-reads it every
// frame, so orbiting the camera mid-bend keeps the lean pointing where the blow came from.
[DisallowMultipleComponent]
public class HitBend : MonoBehaviour
{
    static readonly int BendId = Shader.PropertyToID("_Bend");
    static readonly int BendBaseId = Shader.PropertyToID("_BendBase");
    static readonly int BendSpanId = Shader.PropertyToID("_BendSpan");

    [Tooltip("How far the TOP of the sprite is thrown, as a fraction of the sprite's own height. A fraction " +
             "rather than world units, so one setting fits a sapling and an oak.")]
    [SerializeField, Range(0f, 1f)] float lean = 0.18f;

    [Tooltip("How long the whole wobble lasts, in seconds — the lurch and everything after it.")]
    [SerializeField, Min(0.05f)] float duration = 0.5f;

    [Tooltip("How many times it swings past upright on the way back. 0 = it simply eases home; more is springier " +
             "and lighter, which suits a bush more than a trunk.")]
    [SerializeField, Min(0f)] float wobbles = 1.5f;

    [Tooltip("On the blow that KILLS it, the tree plays this same flinch — same strength, same speed, same " +
             "shape — and the body is simply taken away partway through. This is how much of it plays: 1 = all " +
             "of it, and small numbers cut it while it is still leaning away, before it can swing back.")]
    [SerializeField, Range(0.01f, 1f)] float deathCut = 0.15f;

    [Tooltip("Left empty = the sprites under this object. They all bend together, by the same amount.")]
    [SerializeField] SpriteRenderer[] renderers;

    Damageable _damageable;
    MaterialPropertyBlock _mpb;
    Sprite[] _spanFor;     // which sprite each renderer's pushed base/span was measured from

    float _elapsed;        // seconds into the wobble
    float _span;           // how long a WHOLE wobble takes — the shape is read against this, always
    float _window;         // how much of it actually gets played: all of it, or up to the cut on a felling
    float _amount;         // this blow's lean; the SIGN is resolved per frame, against the camera
    float _wobbles;        // this blow's swings — a felling has none, it only goes over
    bool _dying;           // the body is going away at the end of this one, so it never comes back upright

    // The blow's direction on the ground, pointing AWAY from whoever struck. Kept in world space rather than
    // resolved to the sprite's axis once, because the camera can be turned while the tree is still swaying and
    // the lean has to keep meaning the same thing on the ground.
    Vector3 _away;

    void Awake()
    {
        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<SpriteRenderer>(true);

        _mpb = new MaterialPropertyBlock();
        _spanFor = new Sprite[renderers.Length];

        _damageable = GetComponent<Damageable>();
        if (_damageable == null)
            Debug.LogError($"[{nameof(HitBend)}] no {nameof(Damageable)} on this object — nothing to bend on.", this);
    }

    void OnEnable()
    {
        if (_damageable == null) return;
        _damageable.Damaged += Struck;
        _damageable.Died += Felled;
        _damageable.Knocked += Shoved;
    }

    void OnDisable()
    {
        if (_damageable != null)
        {
            _damageable.Damaged -= Struck;
            _damageable.Died -= Felled;
            _damageable.Knocked -= Shoved;
        }

        // A pooled object must not come back mid-lurch.
        _window = 0f;
        Push(0f);
    }

    // THE SAME DIRECTION THE BLOW SHOVES IN. Every attack already decides this and states it once, for its
    // knockback: a sword pushes out from whoever swung it, a wave pushes along the way it is flying. So the
    // lean is not a second opinion about where the blow came from — it is the blow's own answer, and an attack
    // that changes its mind about the shove changes the lean with it, for free.
    //
    // Raised even when the body cannot be moved, which is what makes it usable here: a tree is never shifted an
    // inch by a shove, and the direction of that shove is exactly what it should be leaning away from.
    void Shoved(Vector3 push)
    {
        push.y = 0f;
        if (push.sqrMagnitude > 1e-6f) _away = push.normalized;
    }

    // Where the blow came from, for anything that lands with NO shove at all — a blade authored at zero
    // knockback still bends what it hits. The source is the component that dealt it, so its position is where
    // the blow came from.
    //
    // A blow with no source, or one landing dead centre, leaves the last direction in place rather than
    // resolving to zero — the tree still reacts, it just reacts the way it was last pushed.
    void Struck(object source)
    {
        Aim(source);
        Swing(duration);
    }

    void Aim(object source)
    {
        if (source is Component c)
        {
            Vector3 away = transform.position - c.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude > 1e-6f) _away = away.normalized;
        }
    }

    // THE BLOW THAT KILLS IT, and the throe runs for exactly as long as the body has been given — asked of the
    // Damageable rather than authored again here, because the two would otherwise have to be kept level by hand
    // and the day they drifted the tree would either snap out of existence mid-lurch or stand there finished,
    // waiting. A body that vanishes at once simply has no throe, which is the honest reading of no time.
    void Felled(object source)
    {
        Aim(source);

        float window = duration * deathCut;
        Swing(duration, window, dying: true);

        // The body goes when the motion does. Asked for rather than agreed with, so the two cannot drift.
        _damageable.DelayVanish(window);
    }

    void Swing(float span, float window = -1f, bool dying = false)
    {
        if (span <= 0f) return;

        _amount = lean;
        _span = span;
        _wobbles = wobbles;
        _window = window > 0f ? window : span;
        _dying = dying;
        _elapsed = 0f;
    }

    void Update()
    {
        if (_window <= 0f) return;

        _elapsed += Time.deltaTime;

        // Out of window. A flinch straightens up; a felling is cut off wherever it had got to, and the body is
        // going away on this same frame — straightening it first would undo the whole thing on its last frame.
        if (_elapsed >= _window)
        {
            _window = 0f;
            if (!_dying) Push(0f);
            return;
        }

        // AGAINST THE WHOLE WOBBLE, never against the window. This is what makes a felling run at flinch speed
        // instead of stretching the same motion over a different length of time: the shape is the shape, and
        // the window only decides how much of it is seen.
        float p = _elapsed / _span;

        // A DECAYING SWING: full throw at the moment of the blow, dying to nothing by the end. cos rather than
        // sin so it starts AT the extreme instead of travelling out to it — the lurch is the impact, and a tree
        // that eased into its own recoil would look like it was bracing rather than being hit.
        Push(_amount * Mathf.Cos(_wobbles * 2f * Mathf.PI * p) * (1f - p));
    }

    // Signed against the sprite's own sideways, resolved fresh every frame: the art is billboarded, so which
    // way its local +X points on the ground depends on where the camera is standing.
    void Push(float amount)
    {
        if (renderers == null) return;

        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;

            r.GetPropertyBlock(_mpb);          // read-modify-write: HitFlash and the fade share this block

            // Writes the base and span into the block as a side effect, so they go out with the lean below.
            float span = SpanOf(r, i);

            // HOW MUCH OF THE BLOW WAS SIDEWAYS ON SCREEN, signed. The renderer's own right, flattened onto the
            // ground: a tree struck along the camera's line of sight has nowhere to lean that the player could
            // read, and throwing it fully to one side would be inventing a direction the blow never had. A
            // mirrored sprite carries a negative X scale, which flips this vector with it — so the lean lands on
            // the correct side of a flipped character with nothing here having to know it was flipped.
            Vector3 right = r.transform.right;
            right.y = 0f;
            float sideways = right.sqrMagnitude > 1e-6f ? Vector3.Dot(_away, right.normalized) : 0f;

            _mpb.SetFloat(BendId, amount * sideways * span);
            r.SetPropertyBlock(_mpb);
        }
    }

    // The sprite's own height in object space, pushed alongside so the shader can turn a vertex's Y into "how
    // far up the sprite am I". Measured from the sprite's bounds rather than authored, and only when the sprite
    // changes — for a tree that is never. The same trick FadeWhenBlocking uses for its ramp, for the same
    // reason: a sprite's UVs are a sub-rect of its sheet, so there is no 0..1 "up" coordinate in the shader.
    float SpanOf(SpriteRenderer r, int index)
    {
        var sprite = r.sprite;
        if (sprite == null) return 0f;

        Bounds local = sprite.bounds;
        float span = local.extents.y * 2f;

        if (_spanFor[index] != sprite)
        {
            _spanFor[index] = sprite;
            _mpb.SetFloat(BendBaseId, local.center.y - local.extents.y);
            _mpb.SetFloat(BendSpanId, span);
        }
        return span;
    }
}
