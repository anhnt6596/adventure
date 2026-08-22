using UnityEngine;

// What a unit is in the middle of. The DURATION of an action, not its cooldown — a cooldown gates the one
// thing that is cooling and nothing else, while this is the window where the unit is committed.
//
// The two are not equally interruptible, and that asymmetry is the rule:
//
//     Attack  — a skill may cut it short. Cancelling into a dash is a move, not an exploit.
//     Skill   — nothing cuts it short. Once it starts it finishes.
//     Dash    — travel. An attack may cut it short, and nothing else may: swinging out of a lunge is the
//               other half of the move that lunges out of a swing, and the pair is what makes the two
//               buttons read as one hand. A second skill on top of it would be two things owning the body.
public enum ActionKind { None, Attack, Skill, Dash }

// Base for a DYNAMIC unit — one that moves and attacks. The control surface a view/animator reads (Velocity,
// IsBusy, Facing) lives here, so ONE view drives the player and an enemy alike. Whatever FEEDS Move
// stays external (player input, enemy AI). Subclasses supply the stat numbers by overriding the accessors. A
// static thing (Prop) is a plain Unit — it never runs this loop.
public abstract class DynamicUnit : Unit
{
    protected CollisionBody body;   // the unit's body, auto-found under it — not wired by hand

    Vector2 _input;
    Vector2 _steer;         // what the last Update read out of _input — see SteerDir
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
    // nothing left for a second check to add), and NEITHER DOES A LUNGE — the attack cuts it, which is the
    // whole of the rule. The recovery still gates it: cutting a dash short buys an attack you were already
    // owed, not a free one.
    public bool CanAttack => _cooldownTimer <= 0f && Busy != ActionKind.Skill;

    // ...and the other direction is deliberately NOT symmetric: a swing can be cancelled into a skill, a
    // skill cannot be cancelled into anything, and a lunge can only be cancelled into an attack.
    public bool CanUseSkill => Busy == ActionKind.None || Busy == ActionKind.Attack;

    // The two rules above, chosen by what is about to be thrown — so an ability asks one question instead of
    // remembering which of them applies to it. Commit is this plus the hold.
    public bool CanDo(ActionKind kind) => kind == ActionKind.Attack ? CanAttack : CanUseSkill;

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

    // WHICH WAY THE UNIT IS BEING STEERED this instant — the held keys, the AI's pursuit — as a unit vector on
    // XZ, or ZERO when nothing is steering it.
    //
    // A SECOND DIRECTION, AND THE POINT IS THAT IT IS NOT THE FACING. The facing is where the unit is AIMED:
    // the cursor sets it outright, and whatever is committed owns it until it ends. Steering is where the
    // player is asking the body to GO. They agree while walking and nowhere else — aim a blow at something and
    // hold the keys the other way and they are opposites, which is exactly the moment an ability has to know
    // which of the two it wants. Storing one direction for both is what made a dash leave along the swing.
    //
    // KEPT WHILE COMMITTED, because the input is: Move is taken mid-swing and mid-lunge and only the TRAVEL is
    // thrown away (see Update), so the unit always knows which way the player is leaning even while it is
    // standing still.
    //
    // Read off the live accumulation when there is one and off the last frame otherwise, so it answers the
    // same whenever in the frame it is asked — an ability thrown in the same frame as the input (MCInput
    // steers, then presses) sees this frame's keys, and one asked after Update has cleared them still does.
    public Vector3 SteerDir
    {
        get
        {
            Vector2 held = _input.sqrMagnitude > 0.0001f ? _input : _steer;
            return held.sqrMagnitude > 0.0001f ? new Vector3(held.x, 0f, held.y).normalized : Vector3.zero;
        }
    }

    // WHERE THIS BODY WOULD GO if it were free right now: the steering if there is any, the aim if there is
    // not. What every TRAVEL move leaves along — a dash today, anything else that carries the body later.
    //
    // The keys win because they are the plainer statement. A cursor is left wherever it was last needed and
    // goes on meaning something long after the player stopped thinking about it; a key is only held while a
    // finger is on it. So "I am pointing at the thing I am hitting AND holding away from it" reads as one
    // sentence: hit that, go this way. A dash that answered the cursor there would be leaping into the fight
    // the player was asking to leave.
    public Vector3 TravelDir => SteerDir.sqrMagnitude > 1e-6f ? SteerDir : FacingDir;

    // The numbers the control loop needs; each unit kind sources them differently.
    protected abstract float MoveSpeed { get; }
    protected abstract float AttackSpeed { get; }
    protected abstract float Mass { get; }

    // THE REST AFTER A BLOW, in BASE seconds at 1x attack speed — Hold divides it by the rate when it charges
    // the recovery, so a fast swinger recovers fast. One attack costs its own window plus this.
    //
    // THE ATTACK'S, AND ONLY THE ATTACK'S. A skill's wait is the skill's own number, authored on the prefab
    // that carries it — including the dash's, which is its lunge plus a breath it names itself (DashSkill).
    // A body-wide "gap between actions" would tie the two together, and then swinging faster would also mean
    // dashing more often.
    public abstract float Recovery { get; }

    // The unit's top speed, readable from outside. What something STEERING the unit needs in order to size a
    // step to the ground left in front of it. The stat itself stays protected; this is the number.
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
    // A COMMITMENT REPLACES the window rather than extending it, and that is what makes a cancel real: an
    // action starting mid-swing or mid-lunge must end that one NOW, not leave the longer of the two locks
    // running past the thing that interrupted it. Whoever is refused a cancel is refused by CanDo, before
    // ever getting here — so anything that reaches this line has already won the body.
    public void Hold(float seconds, ActionKind kind)
    {
        _busyTimer = seconds;
        _busyKind = kind;
        Commitment++;

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
        _cooldownTimer = seconds + Recovery / AttackRate;
        _cooldownTotal = _cooldownTimer;
    }

    // WHICH commitment is running, counted up on every one. An action that owns the body over TIME — a lunge
    // moving the transform itself, frame by frame — has to be able to tell that something cut in and took the
    // body off it, and a kind cannot say that: an attack cutting into an attack is still Attack. Watching this
    // number is how such an action knows to stop, and it is the same answer for every way of being cut short.
    public int Commitment { get; private set; }

    // ---- being carried ------------------------------------------------------------------------------
    //
    // A GLIDE: the body carried along a direction, so far over so long, while whatever asked for it plays out.
    // A dash and a blow that steps in are the same movement with different numbers around it — so the movement
    // lives HERE, once, on the thing that owns the transform, instead of being written again inside every
    // ability that wants to travel. What each of them adds on top (mass, after-images, a hitbox) is what makes
    // them different skills; sliding is not.
    //
    // TIED TO THE COMMITMENT that was running when it started, so whatever cuts that action short cuts this
    // short with it: a swing thrown out of a lunge does not leave the body still sliding underneath it, and
    // nothing has to remember to say so. A shove cancels it too — being knocked off your feet is a way of
    // being stopped.
    //
    // REPLACES rather than adds. Two things carrying one body at once is the same fight two things writing one
    // position is, and the newer claim is the one that just won the commitment.
    Vector3 _glideDir;
    float _glideLeft;      // seconds
    float _glideSpeed;
    int _glideOwner;       // the Commitment this was granted under

    public void Glide(Vector3 direction, float distance, float seconds)
    {
        direction.y = 0f;
        if (distance <= 0f || seconds <= 0f || direction.sqrMagnitude < 1e-6f) { _glideLeft = 0f; return; }

        _glideDir = direction.normalized;
        _glideSpeed = distance / seconds;
        _glideLeft = seconds;
        _glideOwner = Commitment;
    }

    // THE SAME CLAMP A KNOCKBACK SLIDE GETS, for the same reason: the world only pushes a body back out of a
    // wall while its centre is within its own radius of one, so a step longer than that jumps clean through and
    // lands inside. Being carried is the fastest anything in the game moves, which makes it the most likely
    // thing to tunnel — and it is why a lunge into a wall covers less ground than the same lunge into the open,
    // which is what it should look like.
    void StepGlide(float dt)
    {
        if (_glideLeft <= 0f) return;
        if (Commitment != _glideOwner) { _glideLeft = 0f; return; }

        dt = Mathf.Min(dt, _glideLeft);
        _glideLeft -= dt;

        Vector3 step = _glideDir * (_glideSpeed * dt);
        float max = body != null ? body.Radius * 0.9f : float.MaxValue;
        if (step.sqrMagnitude > max * max) step = step.normalized * max;

        transform.position += step;
    }

    // LENGTHEN THE RECOVERY that is already running — for a blow that costs more than its own swing. Added
    // rather than assigned, so it lands on top of whatever the action itself charged instead of replacing it.
    //
    // BASE SECONDS, divided by the rate here, the same way Recovery is: everything about how fast a
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
        if (!CanDo(kind)) return false;

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
        _steer = move;          // held past the clear, so SteerDir reads the same all frame
        _input = Vector2.zero;

        bool aimed = _aimed;   // cleared here and not below, so an early return cannot carry it into next frame
        _aimed = false;

        // While a knockback shove is carrying the body, it drives movement — don't fight it with input, and
        // drop whatever was carrying the body before it: a shove is what stops a lunge.
        if (body != null && body.IsKnocked) { _glideLeft = 0f; Velocity = Vector3.zero; return; }

        // BEFORE the commitment check, because being carried is the one kind of movement that happens WHILE
        // committed — it is the action moving the body, not the player steering it.
        StepGlide(Time.deltaTime);

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
