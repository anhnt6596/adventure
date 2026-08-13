using UnityEngine;

// Springs AWAY from what the character is looking at and puts a knife into it on the way out. One press, one
// skill, one class — a slot holds exactly one CharacterSkill (MCInput says so out loud), so a press that both
// moves the body and throws something has to be a single thing rather than two bolted together.
//
// THE CHARACTER DOES NOT TURN ROUND. It keeps looking where it looked and travels backwards, which is what
// makes this a retreat instead of a dash the other way — and it is what leaves the throw aimed at whatever is
// being backed away from. A backstep that faced its escape route would be running away and shooting the
// scenery.
//
// A DashSkill, because that is what the movement IS: the same lunge, the same mass while it lasts, the same
// invulnerability window and the same after-images. All this adds is which way (one line) and what leaves the
// hand. Reimplementing the lunge to change its sign would be a hundred lines copied for a minus.
//
// THE THROW IS AT THE START, not on a hit frame. "As it goes" is the design — the knife and the leap are one
// beat — and hanging it on the clip would mean the skill silently did half its job on any character whose dash
// clip nobody remembered to mark.
public class BackstepSkill : DashSkill
{
    [Header("Backstep")]
    [Tooltip("Where the knife comes out — the hand. Empty = this object.")]
    [SerializeField] Transform muzzle;

    [Tooltip("What flies. Any Projectile: this hands it a Shot and never learns what it is.")]
    [SerializeField] Projectile projectile;

    [Tooltip("World units per second.")]
    [SerializeField, Min(0f)] float speed = 10f;

    [Tooltip("How long it stays in the air, in seconds. How FAR it gets falls out of this and the speed, so " +
             "anything that makes the shot faster also makes it reach further.")]
    [SerializeField, Min(0f)] float life = 0.7f;

    [Tooltip("A MULTIPLE of the caster's attack power, not a flat number. 1 = a normal hit.")]
    [SerializeField, Min(0f)] float power = 1f;

    [Tooltip("Shove dealt to what it hits; 0 = none.")]
    [SerializeField, Min(0f)] float knockback = 0f;

    [Tooltip("How many go out, and the degrees between neighbours. An odd count puts one straight down the " +
             "facing; an even one straddles it.")]
    [SerializeField, Min(1)] int count = 1;
    [SerializeField, Min(0f)] float spread = 22.5f;

    // Named apart from the dash's own distance and duration, which this still carries and a node can still
    // move. Four more numbers a tree can reach, on the same key.
    public const string Speed = "throw-speed";
    public const string Life = "throw-life";
    public const string Power = "throw-power";
    public const string Knockback = "throw-knockback";
    public const string Count = "throw-count";
    public const string Spread = "throw-spread";

    Stat _speed, _life, _power, _knockback, _count, _spread;

    Vector3 Muzzle => muzzle != null ? muzzle.position : transform.position;

    // The one thing the movement needed to say differently.
    protected override Vector3 LungeDir => -Owner.FacingDir;

    protected override void Awake()
    {
        base.Awake();

        _speed = Tunable(Speed, speed);
        _life = Tunable(Life, life);
        _power = Tunable(Power, power);
        _knockback = Tunable(Knockback, knockback);
        _count = Tunable(Count, count);
        _spread = Tunable(Spread, spread);
    }

    void Start()
    {
        if (projectile == null)
            Debug.LogError($"[{nameof(BackstepSkill)}] no {nameof(Projectile)} assigned — it will leap and " +
                           "throw nothing.", this);
    }

    // Gated on the lunge: a dash that refused — already mid-flight, no body to move — is a press that did not
    // happen, and a press that did not happen must not spend a knife.
    protected override bool Run()
    {
        if (!base.Run()) return false;

        Throw();
        return true;
    }

    void Throw()
    {
        if (projectile == null) return;

        // FORWARD. Owner.FacingDir, not the direction the lunge is carrying the body — see the note above.
        // Read here and copied into the Shot, so a buff landing while the knife is in the air cannot bend it.
        float shotSpeed = _speed.Value;
        var centre = new Shot(Owner.FacingDir, Owner.Team, Owner.AttackPower * _power.Value,
                              shotSpeed, _life.Value * shotSpeed, _knockback.Value, Owner);

        Projectile.Fan(projectile, Muzzle, centre, Mathf.RoundToInt(_count.Value), _spread.Value);
    }
}
