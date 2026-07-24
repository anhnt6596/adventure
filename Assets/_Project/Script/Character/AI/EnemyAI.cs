using UnityEngine;

// The shared monster FSM: Idle -> (aggro or hit) -> Chase -> (in range) Attack -> (out) Chase / (too far)
// Forget -> (timeout) Idle. States + transitions are fixed here; each monster subclass supplies the four
// strategies that fill them in (Build). It drives an EnemyController — the real hit lives on the skill it
// triggers, this only decides WHERE to be and WHEN to swing.
[RequireComponent(typeof(EnemyController))]
public abstract class EnemyAI : MonoBehaviour
{
    enum State { Idle, Chase, Attack, Forget }

    const float AttackHysteresis = 1f;   // stay in Attack until the target is this far PAST attackRange — kills the Chase/Attack jitter right at the boundary

    // Each kind picks its behaviours here — the one thing a subclass writes.
    protected abstract AIStrategies Build();

    AIContext _ctx;
    AIStrategies _s;
    Damageable _self;
    State _state;
    float _forgetTimer;

    void Awake()
    {
        _ctx = new AIContext
        {
            controller = GetComponent<EnemyController>(),
        };
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

    void TickIdle()
    {
        _s.Idle.Tick(_ctx);
        var t = _s.Aggro.Detect(_ctx);
        if (t != null) { _ctx.target = t; _state = State.Chase; }
    }

    void TickChase()
    {
        if (!_ctx.HasLiveTarget) { EnterForget(); return; }
        float d = _ctx.DistanceToTarget();
        if (d > _ctx.config.leashRadius) { EnterForget(); return; }
        if (d <= _ctx.AttackRange) { _state = State.Attack; return; }
        _ctx.controller.Move(_s.Pursuit.DirTo(_ctx, _ctx.target.Position));
    }

    void TickAttack()
    {
        if (!_ctx.HasLiveTarget) { EnterForget(); return; }
        float d = _ctx.DistanceToTarget();
        if (d > _ctx.config.leashRadius) { EnterForget(); return; }
        if (d > _ctx.AttackRange + AttackHysteresis) { _state = State.Chase; return; }   // don't bail the instant they step out
        _s.Attack.Tick(_ctx);
    }

    void TickForget()
    {
        // it still remembers the target — resume if the target wanders back within reach
        if (_ctx.HasLiveTarget && _ctx.DistanceToTarget() <= _ctx.config.reEngageRadius) { _state = State.Chase; return; }
        _forgetTimer -= Time.deltaTime;
        if (_forgetTimer <= 0f) { _ctx.target = null; _state = State.Idle; }
    }

    void EnterForget() { _forgetTimer = _ctx.config.forgetTime; _state = State.Forget; }

    // Hit from anywhere -> fight back. Passive monsters enter combat only through this.
    void OnDamaged(object source)
    {
        var attacker = (source as Component)?.GetComponentInParent<IDamageable>();
        _ctx.target = attacker ?? _ctx.FindHostile(_ctx.config != null ? _ctx.config.aggroRadius : 0f);
        if (_ctx.target != null && (_state == State.Idle || _state == State.Forget)) _state = State.Chase;
    }
}
