using UnityEngine;

// The shared monster FSM. Combat is a small loop built around Idle as the DECIDE hub: every time it lands on
// Idle it turns to face the target, then picks move (Chase) or shoot (Attack). After a shot it returns to Idle
// to re-decide — so the unit always RE-AIMS before the next shot (its skill fires along FacingDir). Each
// subclass only supplies the four strategies (Build). The real hit lives on the skill this triggers.
[RequireComponent(typeof(EnemyController))]
public abstract class EnemyAI : MonoBehaviour
{
    enum State { Idle, Chase, Attack, Forget }

    // Each kind picks its behaviours here — the one thing a subclass writes.
    protected abstract AIStrategies Build();

    AIContext _ctx;
    AIStrategies _s;
    Damageable _self;
    State _state;
    float _forgetTimer;
    float _recognize;   // reaction delay left before a freshly-aggressive unit engages (frozen in Idle)

    void Awake()
    {
        _ctx = new AIContext { controller = GetComponent<EnemyController>() };
        _s = Build();
        _self = GetComponentInChildren<Damageable>(true);
    }

    void Start()
    {
        _ctx.config = _ctx.controller.Config;
        _ctx.home = transform.position;   // where EnemySpawner placed it
        _state = State.Idle;
    }

    void OnEnable()  { if (_self != null) _self.Damaged += OnDamaged; }
    void OnDisable() { if (_self != null) _self.Damaged -= OnDamaged; }

    void Update()
    {
        if (_ctx.config == null) return;   // inert without stats
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
            _s.Idle.Tick(_ctx);
            var t = _s.Aggro.Detect(_ctx);
            if (t != null) Acquire(t);
            return;
        }

        if (_recognize > 0f) { _recognize -= Time.deltaTime; return; }   // just turned aggressive -> freeze a beat before engaging

        float d = _ctx.DistanceToTarget();
        if (d > _ctx.config.leashRadius) { EnterForget(); return; }
        _state = d <= _ctx.AttackRange ? State.Attack : State.Chase;
    }

    void TickChase()
    {
        if (!_ctx.HasLiveTarget) { EnterForget(); return; }
        FaceTarget();
        float d = _ctx.DistanceToTarget();
        if (d > _ctx.config.leashRadius) { EnterForget(); return; }
        if (d <= _ctx.AttackRange) { _state = State.Attack; return; }   // reached range -> commit to attacking (NOT via Idle — that ping-pongs)
        _ctx.controller.Move(_s.Pursuit.DirTo(_ctx, _ctx.target.Position));
    }

    void TickAttack()
    {
        if (!_ctx.HasLiveTarget) { EnterForget(); return; }
        float d = _ctx.DistanceToTarget();
        if (d > _ctx.config.leashRadius) { EnterForget(); return; }   // ran clean away -> give up
        FaceTarget();                          // keep aimed — the shot leaves along FacingDir
        if (_ctx.controller.IsBusy) return;    // a shot already wound up ALWAYS finishes (commit) — never bail
        _s.Attack.Tick(_ctx);                  // fire
        _state = State.Chase;                   // attack done -> back to Chase to re-aim + re-decide
    }

    void TickForget()
    {
        // it still remembers the target — resume if the target wanders back within reach
        if (_ctx.HasLiveTarget && _ctx.DistanceToTarget() <= _ctx.config.reEngageRadius) { _state = State.Idle; return; }
        _forgetTimer -= Time.deltaTime;
        if (_forgetTimer <= 0f) { _ctx.target = null; _state = State.Idle; }
    }

    void EnterForget() { _forgetTimer = _ctx.config.forgetTime; _state = State.Forget; }

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
            _recognize = _ctx.config != null ? _ctx.config.recognizeTime : 0f;
        _ctx.target = t;
    }

    // Hit from anywhere -> fight back. Passive monsters enter combat only through this. Route into Idle so the
    // decide hub picks it up (react delay, then face + choose).
    void OnDamaged(object source)
    {
        var attacker = (source as Component)?.GetComponentInParent<IDamageable>();
        Acquire(attacker ?? _ctx.FindHostile(_ctx.config != null ? _ctx.config.aggroRadius : 0f));
        if (_ctx.target != null && _state == State.Forget) _state = State.Idle;
    }
}
