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
// IsBusy, Facing) lives here, so ONE view drives the player and an enemy alike. Whatever FEEDS Move
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

    // The numbers the control loop needs; each unit kind sources them differently. AttackCooldown is BASE
    // seconds, authored at 1x attack speed — Hold divides it by the rate when it charges the recovery.
    protected abstract float MoveSpeed { get; }
    protected abstract float AttackSpeed { get; }
    protected abstract float AttackCooldown { get; }   // seconds between attack STARTS
    protected abstract float Mass { get; }

    // Public and sanitised. A 0 or negative stat would divide the timers to infinity and freeze the swing
    // animation outright, so it reads as 1x. A blow scales its clip by this, which is what keeps the sprite in
    // step with the timers below — including the hit frame that lands the damage.
    public float AttackRate => AttackSpeed > 0f ? AttackSpeed : 1f;

    // Public because a blow reads it off its owner (a ShapeAttack on an enemy deals the enemy's damage,
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

    // TAKEN EVEN WHILE COMMITTED, and Update is what decides what to do with it: a unit in the middle of a swing
    // or a lunge turns to face where it is being steered, but does not travel. Refusing the input here instead
    // would throw away the aim with the movement, and they are not the same thing — the swing is what pins the
    // feet, not the eyes.
    public void Move(Vector2 worldDir) => _input += worldDir;

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

        // COMMITTING AS AN ATTACK COSTS THE ATTACK RECOVERY, whatever the action actually was. This is the one
        // place that decides it, so a swing, a combo step and a lunge used as a combo step all pay the same
        // price for the same claim — none of them has to remember to charge it, and none of them can forget.
        if (kind != ActionKind.Attack) return;

        // The recovery begins where the COMMITMENT ENDS, so the timer spans both: one attack costs its own
        // window plus the gap. Carrying the window inside it is what lets a single countdown express that, and
        // it is why the recovery can never expire mid-swing with no clamp needed to promise it.
        //
        // Cancelling into a skill does NOT refund it: the attack was spent the moment it was thrown, and what a
        // cancel buys is the time back, not the attack back.
        _cooldownTimer = seconds + AttackCooldown / AttackRate;
        _cooldownTotal = _cooldownTimer;
    }

    // COMMIT the unit to an action of this kind for this long — the one door every ability goes through. It
    // refuses on the same terms the unit already states (CanAttack, CanUseSkill), so an ability never has to
    // repeat those rules and cannot disagree with them.
    public bool Commit(float seconds, ActionKind kind)
    {
        if (kind == ActionKind.Attack ? !CanAttack : !CanUseSkill) return false;

        Hold(Mathf.Max(0f, seconds), kind);
        return true;
    }

    // How long until the next attack may start. What a combo counts its idle time from — "stopped attacking"
    // has to mean stopped once you were ABLE to, or a slow swing would time itself out mid-string.
    public float AttackReadyIn => Mathf.Max(0f, _cooldownTimer);

    protected virtual void Update()
    {
        if (_busyTimer > 0f) _busyTimer -= Time.deltaTime;
        if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;

        var move = Vector2.ClampMagnitude(_input, 1f);
        _input = Vector2.zero;

        // While a knockback shove is carrying the body, it drives movement — don't fight it with input.
        if (body != null && body.IsKnocked) { Velocity = Vector3.zero; return; }

        // AIM FIRST, AND ALWAYS. It costs nothing while standing still and it is what lets a player turn into
        // the next blow of a combo instead of being locked facing the last one. Whether the body then MOVES is
        // a separate question, answered immediately below.
        if (move.sqrMagnitude > 0.0001f) Aim(move.x, move.y);

        // Committed: turn on the spot. The feet are the thing the action owns.
        if (IsBusy) { Velocity = Vector3.zero; return; }

        Velocity = new Vector3(move.x, 0f, move.y) * MoveSpeed;
        transform.position += Velocity * Time.deltaTime;
    }
}
