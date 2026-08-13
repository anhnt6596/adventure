using UnityEngine;

// A lunge along the way the character is already facing, heavy while it lasts, with a trail of itself left
// behind.
//
// IT GOES WHERE YOU POINT, NOT WHERE YOU HOLD. FacingDir is the snapped 8-sector aim the sprite is actually
// drawn in (see DynamicUnit.Aim), so the dash lands where the character visibly points rather than up to 22°
// off it — the same reason an attack lane uses it.
//
// THE UNIT IS HELD FOR THE DURATION. It stops steering (Move early-outs while busy) which is what leaves this
// component free to own the position for a moment without the two fighting over the same transform, and it
// stops the view claiming the idle animation back mid-lunge.
//
// MASS GOES UP, and that is the whole feel of it: mass is what decides how far a shove moves you and how hard
// you are to push aside, so a heavy dash ploughs through a crowd instead of bouncing off it. It is multiplied
// from whatever the body is carrying at that moment and put back afterwards, rather than assigned from a stat,
// so it composes with anything else that may be moving mass.
// SEVERAL ARE ALLOWED on one body, the same as ProjectileSkill. This used to be DisallowMultipleComponent,
// back when two lunges on one character could only be a mistake — but a subclass counts as the same component
// to that attribute, so it also refused a character who dashes forward AND backsteps, which is two different
// skills with two cooldowns and every right to sit on one body. What stops a real duplicate is the slot: two
// skills claiming one button is what MCInput refuses, out loud, naming both.
public class DashSkill : CharacterSkill
{
    [Header("Dash")]
    [Tooltip("Which animation this plays. A roll, a blink and a charge are this same skill with another clip " +
             "over them, so the clip is authored rather than fixed by the class.")]
    [SerializeField] AnimAction anim = AnimAction.Dash;

    [Tooltip("How far it carries the character, in world units.")]
    [SerializeField, Min(0.01f)] float distance = 4f;

    [Tooltip("How long that takes. Shorter is snappier and more likely to clip a corner — see the step clamp.")]
    [SerializeField, Min(0.01f)] float duration = 0.18f;

    [Tooltip("Mass while dashing, as a multiple of what the body normally carries.")]
    [SerializeField, Min(0f)] float massMultiplier = 5f;

    [Tooltip("Nothing lands during the lunge. Turns the dash from a way of covering ground into a way of " +
             "getting through something — which is a different skill, so it is a tick rather than always on.")]
    [SerializeField] bool invulnerable = true;

    [Header("Trail")]
    [Tooltip("The character's own sprite. Each after-image is a still copy of whatever it is showing at the " +
             "moment it is dropped, so the trail is always the pose actually being played.")]
    [SerializeField] SpriteRenderer source;

    [SerializeField, Min(0)] int ghosts = 6;
    [SerializeField] Color ghostTint = new Color(0.65f, 0.85f, 1f, 0.55f);

    [Tooltip("How long one after-image takes to fade out. Longer than the dash is fine and usually better — " +
             "the tail then outlives the lunge instead of vanishing with it.")]
    [SerializeField, Min(0.01f)] float ghostFade = 0.25f;

    // The names a node addresses these by. Constants rather than literals typed twice: this is the one place
    // that can be checked against, and a node still has to spell it right by hand — the tree has no idea which
    // character will be carrying which skill, so no dropdown can offer them.
    public const string Distance = "distance";
    public const string Duration = "duration";

    Stat _distance, _duration;

    CollisionBody _body;
    Damageable _damageable;

    bool _dashing;
    float _left;            // of the dash
    float _nextGhost;
    Vector3 _direction;
    float _runDistance, _runDuration;   // this dash's numbers, fixed when it started

    // Both put back as they were FOUND rather than reset to a config value, so whatever else may be holding
    // them — a cheat toggle, another effect — is not undone by this one finishing.
    float _massBefore;
    bool _invulnerableBefore;

    SpriteRenderer[] _trail;
    float[] _trailLeft;
    int _next;
    int _live;              // after-images still fading, so a still character costs nothing

    protected override void Awake()
    {
        base.Awake();

        // The serialized fields are the bases; from here on the dash reads the stats, so a node can move them.
        _distance = Tunable(Distance, distance);
        _duration = Tunable(Duration, duration);

        if (Owner != null)
        {
            _body = Owner.GetComponentInChildren<CollisionBody>();
            _damageable = Owner.GetComponentInChildren<Damageable>();
        }

        if (source == null)
            Debug.LogWarning($"[{nameof(DashSkill)}] no sprite to trail — the dash works, it just leaves " +
                             "nothing behind. Drag the character's SpriteRenderer in.", this);
    }

    // WHICH WAY THE LUNGE GOES, and the only thing about it a variant has ever needed to change. Off the
    // FACING and not the velocity: where a dash takes you is something the player aims, not something the legs
    // decide from wherever they last carried the body.
    protected virtual Vector3 LungeDir => Owner.FacingDir;

    protected override bool Run()
    {
        if (_dashing || Owner == null || _body == null) return false;

        _direction = LungeDir;
        _direction.y = 0f;
        if (_direction.sqrMagnitude < 1e-6f) return false;
        _direction.Normalize();

        // READ ONCE, AT THE START. A dash is a committed action, so a buff landing mid-flight must not bend
        // the arc it is already halfway through — and the hold below is one number that has to agree with the
        // distance it was worked out against. Floored so a debuff cannot divide by zero.
        _runDistance = Mathf.Max(0f, _distance.Value);
        _runDuration = Mathf.Max(0.01f, _duration.Value);

        _dashing = true;
        _left = _runDuration;
        _nextGhost = 0f;

        Owner.Hold(_runDuration, ActionKind.Skill);

        // One call is enough: the view stops touching the animator while a skill holds, and Play leaves a
        // looping action that is already running alone. 1x on purpose — the legs move at the speed the art
        // was drawn for, and lengthening the dash carries the character further rather than more slowly, so
        // stretching the clip to fit the duration would be skating.
        PlayAnim(anim);

        _massBefore = _body.Mass;
        _body.SetMass(_massBefore * massMultiplier);

        if (invulnerable && _damageable != null)
        {
            _invulnerableBefore = _damageable.Invulnerable;
            _damageable.Invulnerable = true;
        }
        return true;
    }

    void Update()
    {
        if (!_dashing && _live == 0) return;   // nothing moving and nothing fading

        float dt = Time.deltaTime;
        if (_dashing) StepDash(dt);
        FadeTrail(dt);
    }

    void StepDash(float dt)
    {
        dt = Mathf.Min(dt, _left);
        _left -= dt;

        Vector3 step = _direction * (_runDistance / _runDuration * dt);

        // The same clamp CollisionBody puts on a knockback slide, for the same reason: the world only pushes a
        // body back out of a wall while its centre is within its own radius of one, so a step longer than that
        // jumps clean through and lands inside. A dash is the fastest a character ever moves, which makes it
        // the most likely thing in the game to tunnel.
        float max = _body.Radius * 0.9f;
        if (step.sqrMagnitude > max * max) step = step.normalized * max;

        Owner.transform.position += step;

        _nextGhost -= dt;
        if (_nextGhost <= 0f)
        {
            DropGhost();
            _nextGhost = _runDuration / Mathf.Max(1, ghosts);
        }

        if (_left > 0f) return;
        EndDash();
    }

    void EndDash()
    {
        _dashing = false;
        if (_body != null) _body.SetMass(_massBefore);
        if (invulnerable && _damageable != null) _damageable.Invulnerable = _invulnerableBefore;
    }

    // A body destroyed or disabled mid-lunge — death, a character switch — must not leave the mass raised on
    // the way out. Restoring is safe when nothing was dashing, because it only runs when something was.
    void OnDisable()
    {
        if (_dashing) EndDash();
    }

    // ---- trail ------------------------------------------------------------------------------------
    //
    // A fixed set of renderers made once and reused, rather than an object per after-image: a dash drops
    // several a second and this is a thing the player will hold down all game.
    //
    // UNPARENTED, because an after-image is a mark left BEHIND — parented to the character it would ride
    // along on the very movement it exists to show. They are cleaned up with this component.
    void EnsureTrail()
    {
        if (_trail != null || source == null || ghosts <= 0) return;

        _trail = new SpriteRenderer[ghosts];
        _trailLeft = new float[ghosts];

        for (int i = 0; i < ghosts; i++)
        {
            var go = new GameObject($"{Owner.name} dash trail {i}") { layer = source.gameObject.layer };
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = source.sharedMaterial;
            sr.enabled = false;
            _trail[i] = sr;
        }
    }

    void DropGhost()
    {
        EnsureTrail();
        if (_trail == null || source == null || source.sprite == null) return;

        int index = _next;
        _next = (_next + 1) % _trail.Length;
        if (_trailLeft[index] <= 0f) _live++;   // reclaiming one still fading does not add to the count

        var sr = _trail[index];
        var t = source.transform;

        // The source's WORLD rotation, taken at this instant: the sprite is billboarded, so this is the copy
        // already turned to face the camera. It then holds that angle while it fades, which for a quarter of a
        // second is invisible unless the camera is being spun at the same time.
        sr.transform.SetPositionAndRotation(t.position, t.rotation);
        sr.transform.localScale = t.lossyScale;

        sr.sprite = source.sprite;
        sr.flipX = source.flipX;
        sr.sortingLayerID = source.sortingLayerID;
        sr.sortingOrder = source.sortingOrder - 1;   // behind the body it came off
        sr.color = ghostTint;
        sr.enabled = true;

        _trailLeft[index] = ghostFade;
    }

    void FadeTrail(float dt)
    {
        if (_trail == null) return;

        for (int i = 0; i < _trail.Length; i++)
        {
            if (_trailLeft[i] <= 0f) continue;

            _trailLeft[i] -= dt;
            if (_trailLeft[i] <= 0f)
            {
                _trail[i].enabled = false;
                _live--;
                continue;
            }

            var color = ghostTint;
            color.a = ghostTint.a * (_trailLeft[i] / ghostFade);
            _trail[i].color = color;
        }
    }

    void OnDestroy()
    {
        if (_trail == null) return;
        foreach (var sr in _trail)
            if (sr != null) Destroy(sr.gameObject);
    }

#if UNITY_EDITOR
    // How far it actually goes, drawn from the body along the way it is aimed — a dash distance is a number
    // you judge against the map, not in your head.
    void OnDrawGizmosSelected()
    {
        var owner = Owner != null ? Owner : GetComponentInParent<DynamicUnit>();
        Vector3 from = owner != null ? owner.transform.position : transform.position;
        Vector3 dir = owner != null ? owner.FacingDir : Vector3.right;

        Gizmos.color = new Color(0.5f, 0.85f, 1f, 0.9f);
        Gizmos.DrawLine(from, from + dir * distance);
    }
#endif
}
