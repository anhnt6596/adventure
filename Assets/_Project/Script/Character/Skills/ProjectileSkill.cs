using UnityEngine;
using Lean.Pool;

// Plays an animation and, on the frame that animation connects, lets one or a fan of projectiles go. That is
// the whole skill, and it is deliberately the whole skill: a knife, a fireball, a thrown net and an arrow
// differ in what flies and in nothing else, so they are one class here and one Projectile prefab each.
//
// THE FAN LIVES HERE, NOT IN THE PROJECTILE. Three knives are three ordinary knives thrown at three angles —
// none of them knows it has company, and the one that flies straight is the same object as the two that do
// not. Put the spread in the projectile and every projectile would have to carry a count it usually ignores.
//
// NOT ABSTRACT, AND THERE IS NO KnifeSkill. A subclass per projectile would be a class whose only content is
// which prefab to spawn — the field below already says that, and says it where the art is, on the prefab. What
// varies between two of these is a Projectile, not a skill.
//
// THE HIT FRAME IS THE ART'S, NOT A TIMER. The moment the knife leaves the hand is a thing you can see in the
// drawing, so it is authored on the clip (CharacterAnimSet.hitFrame) and this only listens. A serialized
// "throwAfter" seconds would be the same fact written twice, and the second copy would be the one that is
// wrong after the animator retimes the swing.
//
// It also holds the unit for exactly as long as the clip runs, measured off the clip rather than authored —
// same reason.
//
// Several are allowed on one body on purpose, unlike the dash: a character whose two slots are both a throw is
// an ordinary character, and they differ only by prefab and slot.
public class ProjectileSkill : CharacterSkill
{
    [Header("Throw")]
    [Tooltip("Which animation this plays. Its hit frame is when the projectile is let go, so the clip needs " +
             "one — a clip with hitFrame -1 plays and throws nothing.")]
    [SerializeField] AnimAction anim = AnimAction.Throw;

    [Tooltip("Where it comes out — the hand, the mouth. Empty = this object.")]
    [SerializeField] Transform muzzle;

    [Tooltip("What flies. Any Projectile: the skill hands it a Shot and never learns what it is.")]
    [SerializeField] Projectile projectile;

    [Header("The shot")]
    [Tooltip("World units per second.")]
    [SerializeField, Min(0f)] float speed = 10f;

    [Tooltip("Scale that speed by the caster's attack speed, so someone who swings twice as fast throws twice " +
             "as fast. Off for anything whose pace is its own — a lobbed net or a drifting flame does not " +
             "travel quicker because its thrower got handier with a sword.")]
    [SerializeField] bool speedFromAttackRate;

    [Tooltip("How long it stays in the air, in seconds. How FAR it gets falls out of this and the speed, so " +
             "anything that makes the shot faster also makes it reach further — throwing harder sends the " +
             "knife further out, which is what a player expects and what a fixed distance quietly denies.")]
    [SerializeField, Min(0f)] float life = 0.7f;

    [Tooltip("A MULTIPLE of the caster's attack power, not a flat number — a skill that ignored the stat " +
             "would be worth less on every character that ever invests in hitting harder. 1 = a normal hit.")]
    [SerializeField, Min(0f)] float power = 1f;

    [Tooltip("Shove dealt to what it hits; 0 = none.")]
    [SerializeField, Min(0f)] float knockback = 0f;

    [Header("The fan")]
    [Tooltip("How many go out on the one hit frame. An odd count puts one straight down the facing; an even " +
             "one straddles it, leaving the middle empty.")]
    [SerializeField, Min(1)] int count = 1;

    [Tooltip("Degrees between NEIGHBOURING shots, not the width of the whole fan — so adding one more widens " +
             "the spray instead of squeezing it. Nothing to do at count 1.")]
    [SerializeField, Min(0f)] float spread = 22.5f;

    // The names a node addresses these by, the same way DashSkill names its own. Every one of the four is a
    // number a tree ought to be able to move: further, faster, harder, and the shove.
    public const string Speed = "speed";
    public const string Life = "life";
    public const string Power = "power";
    public const string Knockback = "knockback";
    public const string Count = "count";
    public const string Spread = "spread";

    Stat _speed, _life, _power, _knockback, _count, _spread;

    // Armed by the press, spent by the hit frame. Without it the skill would throw on ANY play of that clip —
    // including one some other system started — and a clip interrupted before its hit frame would leave the
    // throw owed, to be paid by whatever plays it next.
    bool _armed;

    Vector3 Muzzle => muzzle != null ? muzzle.position : transform.position;

    protected override void Awake()
    {
        base.Awake();

        _speed = Tunable(Speed, speed);
        _life = Tunable(Life, life);
        _power = Tunable(Power, power);
        _knockback = Tunable(Knockback, knockback);

        // Count is a Stat like the rest even though it is whole: "one more knife" is the node everybody writes
        // for a skill like this, and a number a tree cannot reach is a number that gets reimplemented later.
        // It rounds at the throw — an Add of +1 is the honest way to move it, a Mul of +10% buys nothing.
        _count = Tunable(Count, count);
        _spread = Tunable(Spread, spread);
    }

    void OnEnable()
    {
        if (Animator != null) Animator.Hit += OnHitFrame;
    }

    // Disarmed as well as unsubscribed: a body put away mid-throw and pooled back out must not still owe one.
    void OnDisable()
    {
        if (Animator != null) Animator.Hit -= OnHitFrame;
        _armed = false;
    }

    void Start()
    {
        if (projectile == null)
            Debug.LogError($"[{nameof(ProjectileSkill)}] no {nameof(Projectile)} assigned — the animation " +
                           "plays and nothing comes out.", this);
    }

    protected override bool Run()
    {
        if (projectile == null || Owner == null) return false;

        _armed = true;

        // AT THE CHARACTER'S OWN ATTACK SPEED, exactly as a swing is (AttackAbility.Swing). A throw is a blow:
        // it is thrown by the same arms, it is a step of the same combos, and a character whose attacks got
        // quicker while its throws did not would be one whose attack speed stopped working the moment its
        // string was built out of these — which is what Xhef's is.
        PlayAnim(anim, Owner.AttackRate);

        // The clip's own length, so the character is locked for the throw and free the instant it finishes. A
        // clip that is missing measures zero and holds nothing, which is the right failure: the skill still
        // fires, it just does not freeze a character in front of an animation that is not there.
        float length = ClipTime(anim);
        if (length > 0f) Owner.Hold(length, Kind);

        return true;
    }

    void OnHitFrame(AnimAction action)
    {
        if (!_armed || action != anim) return;
        _armed = false;

        // READ AT THE MOMENT OF RELEASE, then copied into the Shot and never read again — see Shot. The dash
        // reads its numbers at the start for the same reason, and here the point of release IS the start.
        // Hoisted out of the loop as well, so every shot in one fan carries identical numbers: a buff landing
        // between two spawns of the same throw would otherwise make the left knife stronger than the right.
        // AttackRate and not the raw stat: it is already floored at 1 for anything with no attack speed at
        // all, so a monster that has never heard of the stat throws at the authored pace instead of standing
        // still.
        float damage = Owner.AttackPower * _power.Value;
        float shotSpeed = _speed.Value * (speedFromAttackRate ? Owner.AttackRate : 1f);
        float shove = _knockback.Value;

        // TIME BECOMES DISTANCE HERE, at the one place that knows both numbers. The projectile is handed a
        // length of ground and counts it down as it goes, which is a thing it can measure exactly and which no
        // stutter or slow frame can lengthen — while the caster still authors the flight as the duration it
        // actually reads as. Every buff to the speed lands in the reach for free.
        float shotRange = _life.Value * shotSpeed;
        var centre = new Shot(Owner.FacingDir, Owner.Team, damage, shotSpeed, shotRange, shove, Owner);
        Projectile.Fan(projectile, Muzzle, centre, Mathf.RoundToInt(_count.Value), _spread.Value);
    }
}
