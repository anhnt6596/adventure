using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Lean.Pool;

// A blade of wind thrown forward: it flies to the end of its reach whatever it meets, cuts everything on the
// way, and cuts each of them exactly once.
//
// IT CARRIES OR IT SPENDS ITSELF, and that is the one switch on it. A wave that carries is a line drawn across
// the ground: what makes it worth casting into a crowd is that the crowd does not stop it. A wave that spends
// itself is a single heavy blow looking for a body, and the crowd is exactly what stops it — so the same
// component covers a gust that sweeps a rank and a pressure blade that hits once and bursts. What does not
// change is the shape, the flight or the widening; only whether it is still there afterwards.
//
// EITHER WAY IT CUTS EACH BODY ONCE, which is why it remembers who: a carrying wave stands on the same body for
// several frames as it passes through, and without the memory it would grind them down at one hit per frame.
// The shape decides how MANY it catches, never how long it lingers.
//
// A BOX, NOT THE CRESCENT IT IS DRAWN AS. What the player reads is a wide, thin thing sweeping forward, and a
// box oriented to the flight says exactly that. A true arc would mean testing a ring segment and an angle —
// buying a curvature nobody can see on something crossing the screen, when the visible facts are "how wide" and
// "how deep". The curve is the drawing's business.
//
// THE CUT WIDENS AS IT GOES, because a wave loses its edge with distance rather than its length. It opens at a
// RATE PER UNIT OF GROUND, not across a fraction of the reach, and that is the difference between a rule and a
// coincidence: reach is speed times life, so two waves off the same blade — one quick, one lingering — would
// otherwise open by the same amount over very different distances, and the same wave would be a different
// weapon for having been thrown harder. Per unit of ground, one metre out is one width, always. Rate 0 gives a
// straight lane, so the widening costs nothing to turn off.
//
// Pooled: every field is (re)set in Launch, the struck list included.
[DisallowMultipleComponent]
public class SlashWave : Projectile
{
    [Tooltip("How wide the cut is as it leaves the blade, across the flight.")]
    [FormerlySerializedAs("widthNear")]
    [SerializeField, Min(0.01f)] float startWidth = 1.5f;

    [Tooltip("How much wider it gets per unit of ground covered. A rate rather than an end width, so a fast " +
             "wave and a slow one off the same blade are the same width at the same distance out. 0 = a " +
             "straight lane.")]
    [SerializeField, Min(0f)] float widenPerUnit = 0.25f;

    [Tooltip("Spend the wave on the first thing it reaches, instead of carrying through. Everything standing " +
             "in the cut at that moment is still hit — two bodies shoulder to shoulder are one contact, and " +
             "which of them the query happened to return first is not something a player can aim at.")]
    [SerializeField] bool stopOnHit;

    [Tooltip("How DEEP the cut is along the flight — the thickness of the line, not its reach. Small: a wave " +
             "is an edge, and this is also the longest it may hop in one frame, so a thin one is tested often.")]
    [SerializeField, Min(0.05f)] float thickness = 0.6f;

    [Tooltip("The child holding the sprite. Turned to the flight, and grown WHOLE as the cut widens — the " +
             "size it is authored at is the size at the near width.")]
    [SerializeField] Transform art;

    [Header("Fade")]
    [Tooltip("How much of the flight is spent fading IN, as a fraction of the reach. The wave arrives rather " +
             "than appearing; keep it short or it is faint over the ground it is meant to threaten. 0 = full " +
             "strength from the blade.")]
    [SerializeField, Range(0f, 0.5f)] float fadeIn = 0.12f;

    [Tooltip("And how much fading OUT at the far end. This is what makes the wave run out of force rather than " +
             "blink off, so the reach reads as a real edge. 0 = it vanishes at full strength.")]
    [SerializeField, Range(0f, 0.5f)] float fadeOut = 0.2f;

    [Tooltip("How solid it gets in the middle, where it holds. Just under 1 keeps a hair of the ground showing " +
             "through, so the wave reads as air rather than as a painted object.")]
    [SerializeField, Range(0f, 1f)] float peakAlpha = 250f / 255f;

#if UNITY_EDITOR
    [Header("Editor only")]
    [Tooltip("How far to DRAW the lane. The real reach is not this and cannot be: it is handed over by whoever " +
             "fires the wave (speed x life), and that lives on the skill, not here. This is a ruler for judging " +
             "the two widths against the map, nothing more.")]
    [SerializeField, Min(0.1f)] float previewRange = 6f;
#endif

    Vector3 _dir;
    float _speed, _range, _damage, _knockback, _traveled;
    Component _source;

    // Across the flight, on the ground. Used by the cut, by the meeting test and by the gizmo alike, so none of
    // them can be turned a different way from the others.
    static Vector3 Perp(Vector3 forward)
    {
        forward.y = 0f;
        forward = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.right;
        return new Vector3(forward.z, 0f, -forward.x);
    }

    // Its own box, so a wave meets a shot across its WIDTH rather than within some circle that would have to be
    // wide enough to be fair to it and would then catch things passing well clear.
    protected override bool Reaches(Vector3 point)
    {
        Vector3 d = point - transform.position;
        d.y = 0f;
        Vector3 right = Perp(_dir);
        return Mathf.Abs(Vector3.Dot(d, _dir)) <= thickness * 0.5f
            && Mathf.Abs(Vector3.Dot(d, right)) <= HalfWidth;
    }

    // How the prefab was BUILT: the tilt that lays the blade on the ground, and the size it was drawn at.
    // Cached before anything has flown, because from here on the live values are these times a heading and a
    // growth, and re-reading them would compound.
    Quaternion _artRestRotation;
    Vector3 _artRestScale;

    // The tint the sprite was authored with. Only its ALPHA is ever touched, so a wave drawn blue stays blue —
    // and cached for the same reason the scale is: this object flies again and again.
    SpriteRenderer _sprite;
    Color _spriteRestColor;

    // Who this wave has already cut. A LIST rather than a set: a wave catches a handful of bodies, and for a
    // handful a linear scan beats hashing them — and it costs no allocation on a pooled object.
    readonly List<IDamageable> _struck = new List<IDamageable>();
    readonly List<IDamageable> _found = new List<IDamageable>();

    void Awake()
    {
        if (art == null) return;
        _artRestRotation = art.localRotation;
        _artRestScale = art.localScale;

        _sprite = art.GetComponentInChildren<SpriteRenderer>();
        if (_sprite != null) _spriteRestColor = _sprite.color;
    }

    // 0 at the blade, 1 where the reach runs out. How far ALONG the flight it is — which is what the fade is
    // shaped by, and deliberately not the width: a wave's look is a thing that happens over its own lifetime,
    // its reach is a thing that happens over the ground.
    float Spread => _range > 0f ? Mathf.Clamp01(_traveled / _range) : 0f;

    // How wide the cut is after this much ground. The one place the widening is stated, so the hitbox, the
    // drawing and the gizmo cannot disagree about it.
    float WidthAt(float distance) => startWidth + widenPerUnit * Mathf.Max(0f, distance);
    float HalfWidth => WidthAt(_traveled) * 0.5f;

    public override void Launch(in Shot shot)
    {
        _dir = shot.Direction;
        _speed = shot.Speed;
        _range = shot.Range;
        _damage = shot.Damage;
        _knockback = shot.Knockback;
        Team = shot.Team;
        _source = shot.Source;
        _traveled = 0f;

        _struck.Clear();   // this object has flown before; last flight's victims are nobody now
        AimArt();
        GrowArt();
        FadeArt();
    }

    void Update()
    {
        CombatWorld.Instance.Rebuild();   // once for the whole move, however many hops it takes

        // WALKED, NOT JUMPED, and it matters more here than for a knife: this thing is deliberately thin, so a
        // fast one testing only where it ended up would step clean over a whole rank between two frames. Hops
        // no longer than the cut is deep mean nothing standing in the lane can be missed.
        //
        // The last hop is CUT to whatever is left of the reach, so it stops exactly where its range ends rather
        // than at the end of the hop that crossed it — otherwise a faster wave would quietly be a longer one.
        float step = _speed * Time.deltaTime;
        bool spent = step >= _range - _traveled;
        if (spent) step = _range - _traveled;

        int hops = Mathf.Max(1, Mathf.CeilToInt(step / thickness));
        float hop = step / hops;

        for (int i = 0; i < hops; i++)
        {
            transform.position += _dir * hop;
            _traveled += hop;

            if (Cut()) return;   // spent on what it just found, and already put away
        }

        GrowArt();
        FadeArt();

        // The far end still gets its blow: the hops above ran and Cut tested where they landed, so something
        // standing exactly on the limit is cut rather than watched from a wave already gone.
        if (spent) LeanPool.Despawn(gameObject);
    }

    // Everything standing in the cut where it is now, minus whoever it has already been through. Returns true
    // if the wave spent itself doing it — in which case it is gone and the caller must not touch it again.
    bool Cut()
    {
        CombatWorld.Instance.OverlapBox(transform.position, _dir, HalfWidth, thickness * 0.5f, Team, _found);

        bool caught = false;
        for (int i = 0; i < _found.Count; i++)
        {
            var victim = _found[i];
            if (victim == null || _struck.Contains(victim)) continue;
            _struck.Add(victim);
            caught = true;

            victim.TakeDamage(_damage, _source != null ? (object)_source : this);

            // ALONG THE FLIGHT, not outward from the wave's centre. A wave hits a rank of bodies as one line
            // and pushes the whole line the way it is travelling; shoving outward from the middle would throw
            // the left of the rank left and the right of it right, which reads as an explosion rather than a
            // cut.
            if (_knockback > 0f) victim.ApplyKnockback(_dir * _knockback);
        }

        // AT THE END OF THE CUT IT IS STANDING IN, not on the first name out of the list — the whole box lands
        // together, and only then is the wave spent.
        if (!stopOnHit || !caught) return false;

        LeanPool.Despawn(gameObject);
        return true;
    }

    // TURN THE DRAWING THE WAY THE CUT GOES. The hitbox is already oriented — OverlapBox is handed the flight
    // direction — so without this the lane and the picture of it disagree the moment the wave is thrown anywhere
    // but east, which on something this wide is plain to see.
    //
    // THE ART, NOT THE ROOT, and yaw only. The prefab lays the blade on the ground at an authored tilt; turning
    // the root would drag that tilt round with it and the same wave would lie flat going one way and stand on
    // its edge going another — exactly what Knife avoids by never turning its root. Unity composes euler angles
    // as Y·X·Z, so a yaw applied on the LEFT of the rest pose comes out as (x, y + heading, z): the authored lie
    // survives untouched.
    void AimArt()
    {
        if (art == null) return;

        // RELATIVE TO EAST, not to north. Atan2(x, z) is the compass heading, clockwise from +Z — but the rest
        // pose is drawn facing EAST, which is the convention every piece of 2D art in the project follows (see
        // DynamicUnit's starting facing, and every attack gizmo that draws to the right). Turning it by the
        // absolute heading would rotate it a further ninety degrees off the pose it was already drawn in, and a
        // wave thrown east would fly sideways to its own picture.
        float yaw = Mathf.Atan2(_dir.x, _dir.z) * Mathf.Rad2Deg - 90f;
        art.localRotation = Quaternion.Euler(0f, yaw, 0f) * _artRestRotation;
    }

    // The drawing grows with the cut, and grows WHOLE. Stretching the one axis the box widens on would warp the
    // blade — a crescent pulled sideways stops being the shape it was drawn as — so all three go up together and
    // the picture stays itself, just bigger. Which is also what a wave looks like as it opens out.
    //
    // AGAINST THE START WIDTH, so the size authored in the prefab is the size it leaves the blade at: whoever
    // drew it drew the wave at its narrowest, and everything past that is this ratio. A rate of 0 therefore
    // leaves the drawing alone entirely.
    void GrowArt()
    {
        if (art == null || startWidth <= 0f) return;

        art.localScale = _artRestScale * (WidthAt(_traveled) / startWidth);
    }

    // IN AT THE NEAR END, OUT AT THE FAR ONE, FULL IN BETWEEN. Two short ramps and a hold, which is what a
    // gust actually does: it gathers, it carries, it spends itself. A single ramp across the whole flight would
    // have it at its faintest exactly where it is most dangerous, and no ramp at all would pop it in and out.
    //
    // THE LOOK ONLY. The cut is the same cut at every alpha — a wave fading out still takes a full share of
    // anybody it reaches, because what it is worth was decided when it was thrown, not by how much of it is
    // left to see. Damage that followed the picture would be a wave that quietly stopped working before it
    // stopped moving, which is not something a player can read or aim.
    void FadeArt()
    {
        if (_sprite == null) return;

        float t = Spread;
        float ramp = 1f;
        if (fadeIn > 0f) ramp = Mathf.Min(ramp, t / fadeIn);
        if (fadeOut > 0f) ramp = Mathf.Min(ramp, (1f - t) / fadeOut);

        var color = _spriteRestColor;
        color.a = _spriteRestColor.a * peakAlpha * Mathf.Clamp01(ramp);
        _sprite.color = color;
    }

#if UNITY_EDITOR
    // THE LANE IT WILL CUT, drawn as the near end, the far end, and the two edges joining them. The widening is
    // the thing that needs seeing — two widths are a pair of numbers until the trapezoid between them is on the
    // map next to a body.
    //
    // Pointed at +X (east) while the game is not running, the same direction ShapeAttack draws toward and for
    // the same reason: the 2D art is authored facing right, so that is the one heading you can judge straight
    // against the sprite sitting in front of you. Once it is flying it draws along the flight, from where it
    // now stands to where its reach runs out — the lane it has LEFT, not the one it started with.
    void OnDrawGizmosSelected()
    {
        bool flying = _range > 0f;
        Vector3 dir = flying ? _dir : Vector3.right;
        float range = flying ? _range - _traveled : previewRange;
        if (range <= 0f) return;

        Vector3 near = transform.position;
        Vector3 far = near + dir * range;

        // The near end keeps whatever width it has RIGHT NOW, so a wave halfway down its flight draws the cut
        // it is making rather than the one it made at the muzzle — and the far end is what the rate will have
        // opened it to by the time it stops.
        float halfNear = flying ? HalfWidth : startWidth * 0.5f;
        float halfFar = WidthAt(flying ? _range : previewRange) * 0.5f;

        Gizmos.color = new Color(0.6f, 0.9f, 1f, 0.9f);
        DrawRect(near, dir, halfNear, thickness * 0.5f);
        DrawRect(far, dir, halfFar, thickness * 0.5f);

        // Down the middle of each side, near edge to far edge: the envelope everything inside will be cut by.
        Vector3 right = Perp(dir);
        Gizmos.color = new Color(0.6f, 0.9f, 1f, 0.45f);
        Gizmos.DrawLine(near + right * halfNear, far + right * halfFar);
        Gizmos.DrawLine(near - right * halfNear, far - right * halfFar);
    }

    static void DrawRect(Vector3 c, Vector3 forward, float halfWidth, float halfLength)
    {
        forward.y = 0f;
        Vector3 fwd = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.right;
        Vector3 right = Perp(fwd);

        Vector3 f = fwd * halfLength, r = right * halfWidth;
        Vector3 a = c - f - r, b = c + f - r, d = c + f + r, e = c - f + r;
        Gizmos.DrawLine(a, b);
        Gizmos.DrawLine(b, d);
        Gizmos.DrawLine(d, e);
        Gizmos.DrawLine(e, a);
    }
#endif
}
