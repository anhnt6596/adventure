using UnityEngine;

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

    void Awake()
    {
        _ctx = new AIContext { controller = GetComponent<EnemyController>() };
        _self = GetComponentInChildren<Damageable>(true);
    }

    void Start()
    {
        _ctx.config = _ctx.controller.Config;   // EnemySpawner injected it before now
        _ctx.home = transform.position;         // where EnemySpawner placed it
        _state = State.Idle;
        _ctx.brain = _s = BuildBrain();   // behaviours read the same copy the FSM does
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
        if (d > _s.leashRadius) { EnterForget(); return; }
        if (d <= _ctx.AttackRange) _state = State.Attack;
        else _state = State.Chase;
    }

    void TickChase()
    {
        if (!_ctx.HasLiveTarget) { EnterForget(); return; }
        FaceTarget();
        float d = _ctx.DistanceToTarget();
        if (d > _s.leashRadius) { EnterForget(); return; }
        // Arrived: hand over AND run the attack in the same frame. Just switching state would spend this frame
        // neither moving nor swinging, and one frame of standing still is one frame of idle art punched into
        // the middle of a run — a visible blip on every single approach.
        if (d <= _ctx.AttackRange) { _state = State.Attack; TickAttack(); return; }

        _ctx.controller.Move(_s.pursuit.DirTo(_ctx, _ctx.target.Position));
    }

    void TickAttack()
    {
        if (!_ctx.HasLiveTarget) { EnterForget(); return; }
        float d = _ctx.DistanceToTarget();
        if (d > _s.leashRadius) { EnterForget(); return; }   // ran clean away -> give up
        FaceTarget();                          // keep aimed — the shot leaves along FacingDir

        // Swinging or recovering: the unit PLANTS. It does not close, does not re-chase. The recovery is
        // meant to be the opening its target gets, and a monster that walks through its own recovery never
        // gives one. This is also what keeps the approach from strobing: the unit only ever moves while it is
        // ready to strike, so reaching range ends the movement in an attack instead of in a stop.
        if (!_ctx.controller.CanAttack) return;

        if (d <= _ctx.AttackRange) { _s.attack.Tick(_ctx); return; }   // loaded and in reach — swing
        _state = State.Chase;                                          // loaded but short — go and get them
    }

    void TickForget()
    {
        // it still remembers the target — resume if the target wanders back within reach
        if (_ctx.HasLiveTarget && _ctx.DistanceToTarget() <= _s.reEngageRadius) { _state = State.Idle; return; }
        _forgetTimer -= Time.deltaTime;
        if (_forgetTimer <= 0f) { _ctx.target = null; _state = State.Idle; }
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
        if (_ctx.target != null && _state == State.Forget) _state = State.Idle;
    }
}
