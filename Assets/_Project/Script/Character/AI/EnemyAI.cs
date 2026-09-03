using UnityEngine;
using VContainer;

// The shared monster FSM. Combat is a small loop built around Idle as the DECIDE hub: every time it lands on
// Idle it turns to face the target, then picks move (Chase) or shoot (Attack). A unit only ever MOVES while
// it is loaded: swinging and recovering are both spent standing, aimed at the target, so the recovery reads
// as the opening it is meant to be. The real hit lives on the skill this triggers.
//
// ONE component for every monster — the four behaviours come from the kind's EnemyBrainConfig asset, so a new
// kind of monster is a new asset, not a new class.
[RequireComponent(typeof(EnemyController))]
public class EnemyAI : MonoBehaviour
{
    enum State { Idle, Chase, Attack, Forget }

    AIContext _ctx;
    EnemyBrainConfig _s;   // this unit's OWN copy of the kind's brain
    Damageable _self;
    State _state;
    float _forgetTimer;
    float _recognize;   // reaction delay left before a freshly-aggressive unit engages (frozen in Idle)

    ITimeOfDay _clock;   // injected through EnemySpawner's scope; only creatures with a body clock read it
    IPlayer _player;     // hunters go straight for it (see HuntAggro); everything else ignores it

    [Inject]
    public void Construct(ITimeOfDay clock, IPlayer player)
    {
        _clock = clock;
        _player = player;
    }

    void Awake()
    {
        _ctx = new AIContext { controller = GetComponent<EnemyController>() };
        _self = GetComponentInChildren<Damageable>(true);

        // Everything this creature can do, found rather than dragged in: a serialized list would be one more
        // thing to forget on every prefab. The FSM picks from it by SLOT, exactly as a player's keys do.
        _ctx.abilities = GetComponentsInChildren<CharacterSkill>(true);
    }

    void Start()
    {
        _ctx.config = _ctx.controller.Config;   // EnemySpawner injected it before now
        _ctx.home = transform.position;         // where EnemySpawner placed it
        _state = State.Idle;
        _ctx.clock = _clock;
        _ctx.player = _player;
        _ctx.brain = _s = BuildBrain();   // behaviours read the same copy the FSM does

        // A creature authored to sleep will instead be awake around the clock if nothing injected the time —
        // wrong in a way that looks like a tuning problem, so say it out loud. Monsters on the default 0..24
        // window don't care and stay quiet.
        if (_ctx.Ability(AbilitySlot.Attack) == null)
            Debug.LogError($"[{nameof(EnemyAI)}] nothing in the Attack slot on this body — it will chase and " +
                           "never land a hit.", this);

        if (_s != null && _s.HasBodyClock && _clock == null)
            Debug.LogWarning($"[{nameof(EnemyAI)}] brain '{_s.name}' wakes only {_s.activeFrom}h–{_s.activeTo}h but no {nameof(ITimeOfDay)} was injected — it will behave as awake all day.", this);
    }

    // Take a private copy of the kind's brain. The asset is ONE object shared by every unit of the kind, and the
    // behaviours keep state (WanderRoam's destination and rest timer) — run them off the asset and a whole pack
    // walks to the same spot on the same schedule, and worse, the asset itself is dirtied on disk. Instantiate on
    // a ScriptableObject deep-copies its managed references, so each unit gets its own behaviour objects.
    EnemyBrainConfig BuildBrain()
    {
        var src = _ctx.config != null ? _ctx.config.brain : null;
        if (src == null)
        {
            string kind = _ctx.config != null ? _ctx.config.Id : "<no config>";
            Debug.LogError($"[{nameof(EnemyAI)}] no brain on {nameof(EnemyConfig)} '{kind}' — assign an {nameof(EnemyBrainConfig)} or this unit just stands there.", this);
            return null;
        }
        if (!src.IsComplete)
        {
            Debug.LogError($"[{nameof(EnemyAI)}] brain '{src.name}' has an empty slot — every one of Idle/Aggro/Pursuit/Attack needs a behaviour picked.", this);
            return null;
        }
        return Instantiate(src);
    }

    void OnEnable()  { if (_self != null) _self.Damaged += OnDamaged; }
    void OnDisable() { if (_self != null) _self.Damaged -= OnDamaged; }

    // The copy is ours alone, so it dies with us — a cloned ScriptableObject is not collected on its own.
    void OnDestroy() { if (_s != null) Destroy(_s); }

    void Update()
    {
        if (_s == null) return;   // no brain (or no config to reach one through) -> stand there; BuildBrain logged why
        switch (_state)
        {
            case State.Idle:   TickIdle();   break;
            case State.Chase:  TickChase();  break;
            case State.Attack: TickAttack(); break;
            case State.Forget: TickForget(); break;
        }
    }

    // A unit that has not committed is not IN a fight — it is twitching at one. It holds no target past the
    // instant it strikes: no chase, no tracking. It turns exactly once, on the frame the bite goes out, and
    // then forgets the whole thing — an ambush predator snaps at what brushes against it and is scenery again
    // before its prey has finished flinching. Everything that actually decides to fight — being struck, or an
    // aggro behaviour picking a target — commits, so this path belongs to the opportunist alone (see
    // PredatorAggro's off-hours bite).
    bool Reflex => !_ctx.committed;

    // Drop the target and the fight with it. Also the only place commitment is cleared: hold it any longer and
    // one hit turns a rooted ambusher into a chaser for the rest of its life.
    void Release()
    {
        _ctx.target = null;
        _ctx.committed = false;
        _state = State.Idle;
    }

    // The engagement is over. Something committed keeps remembering for a while (Forget) in case they come back;
    // an opportunist has nothing to remember, so it just lets go. Keeping these two apart is what makes Reflex an
    // invariant rather than a special case: an uncommitted unit is never left in any state but Idle.
    void Disengage()
    {
        if (Reflex) Release();
        else EnterForget();
    }

    // The decide hub. No target -> wander and watch for one. Has a target -> FACE it, then move or shoot.
    void TickIdle()
    {
        if (!_ctx.HasLiveTarget)
        {
            _s.idle.Tick(_ctx);
            var t = _s.aggro.Detect(_ctx);
            if (t != null) Acquire(t);
            return;
        }

        if (_recognize > 0f) { _recognize -= Time.deltaTime; return; }   // just turned aggressive -> freeze a beat before engaging

        float d = _ctx.DistanceToTarget();
        if (_s.BeyondLeash(d)) { Disengage(); return; }
        if (d <= _ctx.AttackRange) { _state = State.Attack; return; }
        if (Reflex) { Release(); return; }   // out of reach and never its fight — forget it was ever there
        _state = State.Chase;
    }

    void TickChase()
    {
        if (!_ctx.HasLiveTarget) { EnterForget(); return; }
        FaceTarget();
        float d = _ctx.DistanceToTarget();
        if (_s.BeyondLeash(d)) { EnterForget(); return; }
        // Arrived: hand over AND run the attack in the same frame. Just switching state would spend this frame
        // neither moving nor swinging, and one frame of standing still is one frame of idle art punched into
        // the middle of a run — a visible blip on every single approach.
        if (d <= _ctx.AttackRange) { _state = State.Attack; TickAttack(); return; }

        _ctx.controller.Move(_s.pursuit.DirTo(_ctx, _ctx.target.Position));
    }

    void TickAttack()
    {
        if (!_ctx.HasLiveTarget) { Disengage(); return; }
        float d = _ctx.DistanceToTarget();
        if (_s.BeyondLeash(d)) { Disengage(); return; }   // ran clean away -> give up
        if (!Reflex) FaceTarget();             // keep aimed every frame — the shot leaves along FacingDir. A reflex tracks nothing; it aims once, below.

        // Swinging or recovering: the unit PLANTS. It does not close, does not re-chase. The recovery is
        // meant to be the opening its target gets, and a monster that walks through its own recovery never
        // gives one. This is also what keeps the approach from strobing: the unit only ever moves while it is
        // ready to strike, so reaching range ends the movement in an attack instead of in a stop.
        //
        // A reflex does not stand here waiting for its jaws to reload either — it lets go and re-notices later,
        // so it is never holding a target it isn't actively biting. The swing already underway is unaffected:
        // a blow fires off the animation's hit frame and hits by overlap, not off the brain's target.
        if (!_ctx.controller.CanAttack) { if (Reflex) Release(); return; }

        if (d <= _ctx.AttackRange)
        {
            // A reflex aims on exactly one frame: this one. It must, or the bite leaves along whatever heading
            // the creature happened to be left facing — and the hit box is thrown FORWARD (forwardOffset), so
            // prey at its flank would be missed by a mouth that is right on top of it. Turning here and not
            // before is the whole difference between aiming and tracking. Order matters: Face writes FacingDir,
            // then the blow swings along it in the same frame.
            if (Reflex) FaceTarget();
            _s.attack.Tick(_ctx);              // loaded and in reach — swing
            if (Reflex) Release();             // one snap, and it is done: no follow-up, no watching them leave
            return;
        }

        if (Reflex) { Release(); return; }     // drifted out of reach — an opportunist does not go and get them
        _state = State.Chase;                  // loaded but short — go and get them
    }

    void TickForget()
    {
        // it still remembers the target — resume if the target wanders back within reach
        if (_ctx.HasLiveTarget && _ctx.DistanceToTarget() <= _s.reEngageRadius) { _state = State.Idle; return; }
        _forgetTimer -= Time.deltaTime;
        if (_forgetTimer <= 0f) Release();
    }

    void EnterForget() { _forgetTimer = _s.forgetTime; _state = State.Forget; }

    // Turn to face the current target on the ground plane. The skill fires along FacingDir, so THIS is the aim —
    // run every combat frame so a shot always leaves toward where the target is now.
    void FaceTarget()
    {
        if (_ctx.HasLiveTarget)
            _ctx.controller.Face(_ctx.target.Position - _ctx.Tr.position);
    }

    // Take a (possibly new) target. A FRESH aggression — no live target until now — starts the reaction delay
    // that freezes it in Idle; re-targeting mid-fight doesn't (it's already worked up).
    void Acquire(IDamageable t)
    {
        if (!_ctx.HasLiveTarget && t != null)
            _recognize = _s.recognizeTime;
        _ctx.target = t;
    }

    // Hit from anywhere -> fight back. Passive monsters enter combat only through this. Route into Idle so the
    // decide hub picks it up (react delay, then face + choose). The scan radius is the FSM's own, NOT the Aggro
    // behaviour's: a PassiveAggro monster has no sight range but still has to find whoever just shot it.
    void OnDamaged(object source)
    {
        if (_s == null) return;   // no brain -> nothing to react with (Damaged fires regardless of Update's guard)
        var attacker = (source as Component)?.GetComponentInParent<IDamageable>();
        Acquire(attacker ?? _ctx.FindHostile(_s.retaliateRadius));
        if (_ctx.target != null) _ctx.committed = true;   // being hit makes it personal — even a rooted ambusher gives chase now
        if (_ctx.target != null && _state == State.Forget) _state = State.Idle;
    }
}
