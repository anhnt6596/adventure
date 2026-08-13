using System;
using UnityEngine;

// What a unit is in the middle of. The DURATION of an action, not its cooldown — a cooldown gates the one
// thing that is cooling and nothing else, while this is the window where the unit is committed.
//
// The two are not equally interruptible, and that asymmetry is the rule:
//
//     Attack  — a skill may cut it short. Cancelling into a dash is a move, not an exploit.
//     Skill   — nothing cuts it short. Once it starts it finishes.
public enum ActionKind { None, Attack, Skill }

// Base for a DYNAMIC unit — one that moves and attacks. The control surface a view/animator reads (Velocity,
// IsBusy, Attacked, Facing) lives here, so ONE view drives the player and an enemy alike. Whatever FEEDS Move
// stays external (player input, enemy AI). Subclasses supply the stat numbers by overriding the accessors. A
// static thing (Prop) is a plain Unit — it never runs this loop.
public abstract class DynamicUnit : Unit
{
    protected CollisionBody body;   // the unit's body, auto-found under it — not wired by hand

    Vector2 _input;
    float _busyTimer;       // the swing itself — locks the unit out of moving and attacking
    float _cooldownTimer;   // gates the NEXT attack only; the unit is free to move while it runs

    ActionKind _busyKind;
    float _cooldownTotal;   // what _cooldownTimer started from, so a dial can be drawn from the pair

    // Two nested windows. IsBusy is the commitment: something is playing out. The cooldown starts at the same
    // moment but RUNS PAST it — swing first, then recovery — so it always outlives the lock and on its own
    // says everything about when the next attack may start.
    public bool IsBusy => _busyTimer > 0f;

    // WHAT it is in the middle of, which is what decides who may interrupt whom. See ActionKind.
    public ActionKind Busy => _busyTimer > 0f ? _busyKind : ActionKind.None;

    // A skill locks attacking out; a swing does not (its own cooldown already covers the swing, so there is
    // nothing left for a second check to add).
    public bool CanAttack => _cooldownTimer <= 0f && Busy != ActionKind.Skill;

    // ...and the other direction is deliberately NOT symmetric: a swing can be cancelled into a skill, a
    // skill cannot be cancelled into anything.
    public bool CanUseSkill => Busy != ActionKind.Skill;

    // How much of the attack's recovery is left, 1 at the moment it is spent and 0 when it is ready. For a
    // dial on the HUD; the same shape CharacterSkill.CooldownFraction has, so one button can draw either.
    public float AttackCooldownFraction
        => _cooldownTotal > 0f ? Mathf.Clamp01(_cooldownTimer / _cooldownTotal) : 0f;
    public Vector3 Velocity { get; private set; }
    public event Action Attacked;

    // Which way the unit is turned in the WORLD, as an 8-sector index (ViewAngleUtil, clockwise from +Z):
    // its last move direction, held while idle. A view turns this into a screen-relative direction against
    // the camera, so the sprite re-aims when the camera orbits even while the unit stands still.
    // A unit that has not moved or aimed yet starts facing EAST, not north — the 2D art is authored facing
    // right, so this is the pose it was actually painted in, and it is the same direction ShapeAttack's gizmo
    // draws toward in the editor. All three of these must agree from the very first frame, not just after the
    // first Aim(): Facing is the sector, FacingDir is that sector as a vector, AimRaw is the unsnapped aim.
    const int EastSector = 2;   // sectors run clockwise from +Z, so 2 * 45° = +X

    public int Facing { get; private set; } = EastSector;
    public Vector3 FacingDir { get; private set; } = SectorDir(EastSector);   // last move direction (world XZ) — where the unit is aimed

    // The same aim BEFORE it was snapped to a sector. Kept because two-direction art has no up or down pose:
    // sectors 0 and 4 straddle the vertical, so the sector index alone cannot say which way such a sprite
    // should face, and only the unsnapped direction still knows.
    public Vector3 AimRaw { get; private set; } = SectorDir(EastSector);

    // The numbers the control loop needs; each unit kind sources them differently. Both attack numbers are
    // BASE seconds, authored at 1x attack speed — Attack() divides them by the rate.
    protected abstract float MoveSpeed { get; }
    protected abstract float AttackSpeed { get; }
    protected abstract float AttackCooldown { get; }   // seconds between attack STARTS
    protected abstract float Mass { get; }

    // Public and sanitised. A 0 or negative stat would divide the timers to infinity and freeze the swing
    // animation outright, so it reads as 1x. The view scales the attack clip by this, which is what keeps the
    // sprite in step with the timers below — including the hit frame that lands the damage.
    public float AttackRate => AttackSpeed > 0f ? AttackSpeed : 1f;

    // How long the swing locks the unit, measured from the attack ANIMATION and pushed in by the view. There
    // is deliberately no config number for it: the lock and the swing are one thing seen twice, so a second
    // hand-typed value could only ever agree or be a bug. Wanting a longer gap between attacks is
    // AttackCooldown's job — and that one leaves the unit free to move, which a longer lock would not.
    float _swingDuration;

    public void SetSwingClipLength(float seconds) => _swingDuration = Mathf.Max(0f, seconds);

    // Public because attack skills read it off their owner (a ShapeAttack on an enemy deals the enemy's damage,
    // the same one on the MC deals the MC's) — the number's source differs per kind, the skill doesn't care.
    public abstract float AttackPower { get; }

    // Virtual so a unit whose stats aren't ready at Start (e.g. an enemy configured after spawn) can defer it.
    protected virtual void Start()
    {
        body = GetComponentInChildren<CollisionBody>();
        if (body == null)
        {
            Debug.LogError($"[{GetType().Name}] no CollisionBody found — no collision, no mass.", this);
            return;
        }
        // TEMP: mass is set once from base stats. Later it becomes dynamic (gear, upgrades, buffs) and should
        // be recomputed on change, not this one-shot at Start. (The body registers itself via its OnEnable.)
        body.SetMass(Mass);
    }

    public void Move(Vector2 worldDir)
    {
        if (IsBusy) return;
        _input += worldDir;
    }

    // Aim the facing at a world direction WITHOUT moving — for a standing attack that must face its target
    // first (its skill fires along FacingDir). No-op on a zero direction so it holds the last aim.
    public void Face(Vector3 worldDir)
    {
        worldDir.y = 0f;
        if (worldDir.sqrMagnitude < 1e-6f) return;
        Aim(worldDir.x, worldDir.z);
    }

    // The one place the aim is set, so Facing and FacingDir can never drift apart. It SNAPS: FacingDir is
    // derived from the 8-sector the sprite is drawn in, not from the raw direction. A continuous aim under an
    // 8-frame sprite would send the attack shape and the shot up to 22.5° off from where the unit visibly
    // points — invisible on a circle centred on the unit, but plain to see once the hitbox is a lane.
    // MOVEMENT is not snapped: Velocity still follows the raw input, only the aim quantises.
    void Aim(float x, float z)
    {
        AimRaw = new Vector3(x, 0f, z).normalized;
        Facing = ViewAngleUtil.GetViewType8(Mathf.Atan2(x, z) * Mathf.Rad2Deg);
        FacingDir = SectorDir(Facing);
    }

    // Sector n is centred on n * 45°, measured the way GetViewType8 reads it: clockwise from +Z.
    static Vector3 SectorDir(int sector)
    {
        float rad = sector * 45f * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
    }

    // Take the unit over for a moment. A skill that moves the body itself needs the unit to stop steering
    // while it does, or both write the same transform in the same frame and the dash is fought by the walk;
    // and the view has to be told to stop drawing whatever was playing. Busy already means exactly that for a
    // swing, so a skill borrows it rather than inventing a second word for it.
    //
    // A SKILL REPLACES the window rather than extending it, and that is what makes the cancel real: starting
    // one mid-swing must end the swing NOW, not leave the longer of the two locks running past the thing that
    // interrupted it.
    public void Hold(float seconds, ActionKind kind)
    {
        _busyTimer = kind == ActionKind.Skill ? seconds : Mathf.Max(_busyTimer, seconds);
        _busyKind = kind;
    }

    // Returns whether the swing actually started, the same promise CharacterSkill.TryUse makes. A refused
    // press is not a silent nothing to whoever asked: it is what the input layer holds on to and offers again
    // when the recovery ends.
    public bool Attack()
    {
        if (!CanAttack) return false;

        // Attack speed scales the swing AND the recovery by the same factor, the way action games have always
        // done it: the ratio between them holds at every rate, so a fast attacker feels like the same attack
        // sped up rather than a different one, and the stat can never go dead.
        float rate = AttackRate;
        float duration = _swingDuration / rate;

        _busyTimer = duration;
        _busyKind = ActionKind.Attack;

        // The cooldown is the recovery that begins where the SWING ENDS, so the timer spans both: one attack
        // costs duration + cooldown. Carrying the swing inside it is what lets a single countdown express that
        // — and it is why the cooldown can never expire mid-swing, with no clamp needed to promise it.
        //
        // Cancelling the swing into a skill does NOT refund it. The attack was spent the moment it was thrown;
        // what a cancel buys is the time back, not the attack back.
        _cooldownTimer = duration + AttackCooldown / rate;
        _cooldownTotal = _cooldownTimer;
        Attacked?.Invoke();
        return true;
    }

    protected virtual void Update()
    {
        if (_busyTimer > 0f) _busyTimer -= Time.deltaTime;
        if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;

        var move = Vector2.ClampMagnitude(_input, 1f);
        _input = Vector2.zero;

        // While a knockback shove is carrying the body, it drives movement — don't fight it with input.
        if (body != null && body.IsKnocked) { Velocity = Vector3.zero; return; }

        if (move.sqrMagnitude > 0.0001f) Aim(move.x, move.y);

        Velocity = new Vector3(move.x, 0f, move.y) * MoveSpeed;
        transform.position += Velocity * Time.deltaTime;
    }
}
