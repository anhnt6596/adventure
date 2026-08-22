using UnityEngine;

// A lunge along the way the character is already facing, heavy while it lasts, with a trail of itself left
// behind.
//
// IT CAN BE SWUNG OUT OF. An attack pressed mid-lunge cuts it there and then — see StepDash — which is the
// other half of the cancel that has always let a swing be dashed out of. What the cut does NOT do is refund
// the dash: the wait was charged the moment it went, and cutting it short buys the time, not the dash.
//
// IT GOES WHERE YOU HOLD, AND ONLY WHERE YOU POINT IF YOU ARE HOLDING NOTHING — DynamicUnit.TravelDir. A
// dash is travel, and the keys are what the player is using to say where the body goes; the cursor says where
// the blows land. Swing at something, hold away from it, dash: the lunge leaves along the keys, because that
// is the half of the sentence that was about going somewhere.
//
// CONTINUOUS EITHER WAY. Both directions are true angles rather than one of the sprite's eight poses, so the
// lunge lands exactly where it was asked for rather than on the nearest pose — the same reason an attack lane
// uses one.
//
// THE UNIT IS HELD FOR THE DURATION, and the unit is what CARRIES it: this asks for a glide and the body does
// the travelling (DynamicUnit.Glide), so there is only ever one thing writing the position. The hold is what
// stops the legs steering against it, and what stops the view claiming the idle animation back mid-lunge.
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

    [Tooltip("The breath AFTER the lunge, before it can be thrown again — the dash's whole wait is this plus " +
             "the duration above. Its own number and not the character's attack recovery: how quickly you " +
             "swing has nothing to say about how soon you may lunge again.")]
    [SerializeField, Min(0f)] float rest = 0.2f;

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
    public const string Rest = "rest";

    Stat _distance, _duration, _rest;

    CollisionBody _body;
    Damageable _damageable;

    bool _dashing;
    float _left;            // of the dash
    float _nextGhost;
    Vector3 _direction;
    float _runDistance, _runDuration;   // this dash's numbers, fixed when it started
    int _commitment;        // the unit's commitment this lunge was granted — see StepDash

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
        _rest = Tunable(Rest, rest);

        if (Owner != null)
        {
            _body = Owner.GetComponentInChildren<CollisionBody>();
            _damageable = Owner.GetComponentInChildren<Damageable>();
        }

        if (source == null)
            Debug.LogWarning($"[{nameof(DashSkill)}] no sprite to trail — the dash works, it just leaves " +
                             "nothing behind. Drag the character's SpriteRenderer in.", this);
    }

    // THE WAIT IS THE LUNGE ITSELF PLUS A BREATH, which is why the inherited Cooldown field means nothing
    // here — a dash is not a spell on a timer. What a player reads as "how often can I dash" is how soon the
    // next one can start once this one has LANDED, so the duration has to be in the number; an authored total
    // would have to be kept in step with it by hand, and would be wrong the first time somebody lengthened the
    // lunge or a node did it for them.
    //
    // The breath is `rest`, this skill's own, and NOT the character's attack recovery: the two are different
    // things that only looked alike while both were small, and one number for both would mean a character who
    // swings faster also dashes more often, which nobody asked for.
    //
    // It is the BASE, so haste still divides it the way it divides any other skill's — a dash slot has no
    // haste stat today, and if one is ever added it shortens this without this line hearing about it.
    protected override float BaseCooldown => Mathf.Max(0.01f, _duration?.Value ?? duration)
                                           + Mathf.Max(0f, _rest?.Value ?? rest);

    // WHICH WAY THE LUNGE GOES. Off TravelDir and not the velocity: the unit is standing still every time this
    // is read — a dash thrown out of a swing has had its feet pinned for the whole swing — so the velocity is
    // zero and says nothing. What the player is HOLDING is a live answer whether or not the body is free to
    // act on it.
    protected virtual Vector3 LungeDir => Owner.TravelDir;

    // WHICH WAY IT LOOKS while it goes, which for anything travelling forwards is the same direction. It has
    // to be said out loud because the sprite is redrawn off the facing every frame, committed or not
    // (UnitView.PushDir): a lunge that left the facing where the last swing put it would be drawn walking
    // backwards for as long as it lasted.
    //
    // A SEPARATE SEAM FROM LungeDir for one reason, and it is the backstep — where the body going one way
    // while the eyes stay on the other IS the move. Two directions, because a lunge really does have two.
    protected virtual Vector3 LungeFacing => _direction;

    protected override bool Run()
    {
        if (_dashing || Owner == null || _body == null) return false;

        _direction = LungeDir;
        _direction.y = 0f;
        if (_direction.sqrMagnitude < 1e-6f) return false;
        _direction.Normalize();

        // TURNED BEFORE ANYTHING ELSE READS IT. Face writes FacingDir, and what a subclass throws as it leaves
        // (BackstepSkill) is aimed off that — so the turn has to be settled before the lunge is under way, not
        // after. It is also what the after-images are copied from.
        Owner.Face(LungeFacing);

        // READ ONCE, AT THE START. A dash is a committed action, so a buff landing mid-flight must not bend
        // the arc it is already halfway through — and the hold below is one number that has to agree with the
        // distance it was worked out against. Floored so a debuff cannot divide by zero.
        _runDistance = Mathf.Max(0f, _distance.Value);
        _runDuration = Mathf.Max(0.01f, _duration.Value);

        _dashing = true;
        _left = _runDuration;
        _nextGhost = 0f;

        // Kind, not a hardcoded one: pressed on its own key this is a DASH, which only an attack may cut, and
        // thrown as a step of a combo it is part of the attack — the difference is who asked, not what a dash is.
        Owner.Hold(_runDuration, Kind);
        _commitment = Owner.Commitment;   // whatever takes this off us cuts the lunge short — see StepDash

        // THE UNIT CARRIES ITSELF. This used to walk the transform frame by frame from out here, wall clamp and
        // all — which is the same thing a stepping blow needs, so it belongs to the body rather than to one of
        // the abilities that asks for it. What is left here is what is actually a dash: the mass, the window,
        // the trail.
        Owner.Glide(_direction, _runDistance, _runDuration);

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
        // CUT SHORT BY WHOEVER TOOK THE BODY. An attack may be thrown out of a lunge, and the swing that
        // answers it owns the unit from that moment. The MOVEMENT already stops on its own — the glide watches
        // this same number — so what this is for is everything the dash raised and nothing else knows to put
        // back: the mass, the invulnerability, the trail that should stop being fed.
        //
        // The commitment number, not the kind: a dash thrown as a step of a combo is committed as an attack,
        // and so is the swing that would cut it.
        if (Owner.Commitment != _commitment) { EndDash(); return; }

        dt = Mathf.Min(dt, _left);
        _left -= dt;

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
