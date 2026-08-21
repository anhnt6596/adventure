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
    bool _aimed;            // something aimed this unit by hand this frame — see Face
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

    // Is a knockback shove carrying the body? While one is, Update throws steering away — so anything driving
    // this unit from outside can tell that its input went nowhere for a reason other than being stuck.
    public bool IsKnocked => body != null && body.IsKnocked;

    // Which way the unit is turned in the WORLD (unit vector on XZ): its last move or aim direction, held
    // while idle. A view turns this into a screen-relative direction against the camera, so the sprite
    // re-aims when the camera orbits even while the unit stands still.
    //
    // CONTINUOUS, NOT ONE OF EIGHT. The sprite has eight poses, the aim does not: a click-to-move order
    // points anywhere at all, and quantising it would send the walk one way and the dash, the shot and the
    // attack lane up to 22.5° another. The eight poses are an approximation of this direction — chosen by
    // whichever anim set is drawing it (CharacterAnimSet.Resolve) — and not the direction itself.
    //
    // A unit that has not moved or aimed yet faces EAST, not north — the 2D art is authored facing right, so
    // this is the pose it was actually painted in, and it is the direction ShapeAttack's gizmo draws toward
    // in the editor.
    public Vector3 FacingDir { get; private set; } = Vector3.right;

    // The numbers the control loop needs; each unit kind sources them differently. AttackCooldown is BASE
    // seconds, authored at 1x attack speed — Hold divides it by the rate when it charges the recovery.
    protected abstract float MoveSpeed { get; }
    protected abstract float AttackSpeed { get; }
    protected abstract float AttackCooldown { get; }   // seconds between attack STARTS
    protected abstract float Mass { get; }

    // The unit's top speed, readable from outside. What something STEERING the unit needs in order to size a
    // step to the ground left in front of it — walking to a point means the last step is a short one, and only
    // the speed says how short (see MoveOrder.Step). The stat itself stays protected; this is the number.
    public float Speed => MoveSpeed;

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

    // TAKEN EVEN WHILE COMMITTED, and Update is what decides what to do with it. A unit in the middle of a swing
    // or a lunge keeps being handed input — it simply neither travels nor turns on it (see Update).
    public void Move(Vector2 worldDir) => _input += worldDir;

    // Aim the facing at a world direction WITHOUT moving — for a blow that must face its target first (it
    // fires along FacingDir). No-op on a zero direction so it holds the last aim.
    //
    // AN AIM SAID OUT LOUD BEATS THE ONE MOVEMENT WOULD IMPLY, and the flag is what makes that true for the
    // REST OF THIS FRAME as well as for the instant: MCInput turns the character at the cursor immediately
    // before the press, and Update runs afterwards — without this the movement input from the same frame
    // would quietly aim it back down the way the player was walking, and the blow would leave along that.
    public void Face(Vector3 worldDir)
    {
        worldDir.y = 0f;
        if (worldDir.sqrMagnitude < 1e-6f) return;
        Aim(worldDir.x, worldDir.z);
        _aimed = true;
    }

    // The one place the aim is set. Normalised, so everything downstream — an attack lane, a lunge, a shot —
    // can take it as a direction and never has to check.
    void Aim(float x, float z)
    {
        FacingDir = new Vector3(x, 0f, z).normalized;
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

    // LENGTHEN THE RECOVERY that is already running — for a blow that costs more than its own swing. Added
    // rather than assigned, so it lands on top of whatever the action itself charged instead of replacing it.
    //
    // BASE SECONDS, divided by the rate here, the same way AttackCooldown is: everything about how fast a
    // character attacks belongs to attack speed, and a number that ignored it would be a growing share of the
    // recovery as the character got quicker — until it was the only thing left and the stat did nothing.
    //
    // The total moves with it so a dial drawn from the pair still reads full at the moment it is charged.
    public void AddAttackRecovery(float baseSeconds)
    {
        if (baseSeconds <= 0f) return;

        float extra = baseSeconds / AttackRate;
        _cooldownTimer += extra;
        _cooldownTotal += extra;
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

        bool aimed = _aimed;   // cleared here and not below, so an early return cannot carry it into next frame
        _aimed = false;

        // While a knockback shove is carrying the body, it drives movement — don't fight it with input.
        if (body != null && body.IsKnocked) { Velocity = Vector3.zero; return; }

        // WALKING AIMS THE UNIT, but it is the weakest claim on the facing there is, and it yields twice:
        //
        //   to an explicit Face() this frame — the cursor said where, and being steered is not an argument
        //     against it;
        //   and to being COMMITTED — an action owns the direction it was thrown in for as long as it lasts.
        //     A blow lands its damage frames later, off the facing as it stands THEN, so a unit that kept
        //     turning on the keys would let a swing be steered after it was thrown, away from what it was
        //     aimed at. Face() still turns it: an enemy tracks its target through its own swing.
        //
        // Between the blows of a combo the unit is free again (the string's gap is recovery, not commitment),
        // so turning into the next one costs nothing and needs no exception here.
        if (!aimed && !IsBusy && move.sqrMagnitude > 0.0001f) Aim(move.x, move.y);

        // Committed: stand still. The feet are the thing the action owns.
        if (IsBusy) { Velocity = Vector3.zero; return; }

        Velocity = new Vector3(move.x, 0f, move.y) * MoveSpeed;
        transform.position += Velocity * Time.deltaTime;
    }
}
