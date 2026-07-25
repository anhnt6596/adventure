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
            if (t != null) _ctx.target = t;
            return;
        }

        FaceTarget();
        float d = _ctx.DistanceToTarget();
        if (d > _ctx.config.leashRadius) { EnterForget(); return; }
        _state = d <= _ctx.AttackRange ? State.Attack : State.Chase;
    }

    void TickChase()
    {
        if (!_ctx.HasLiveTarget) { EnterForget(); return; }
        float d = _ctx.DistanceToTarget();
        if (d > _ctx.config.leashRadius) { EnterForget(); return; }
        if (d <= _ctx.AttackRange) { _state = State.Idle; return; }   // in range -> back to Idle to re-decide (it'll shoot)
        FaceTarget();
        _ctx.controller.Move(_s.Pursuit.DirTo(_ctx, _ctx.target.Position));
    }

    void TickAttack()
    {
        if (!_ctx.HasLiveTarget) { EnterForget(); return; }
        if (_ctx.DistanceToTarget() > _ctx.config.leashRadius) { EnterForget(); return; }   // ran clean away -> give up

        // Once it commits to attacking, COMMIT: fire regardless of range — a fast target outruns a "+1 range"
        // grace every time, so range must not cancel a shot in progress. Only the leash above calls it off.
        FaceTarget();                          // keep aimed while the shot is wound up
        if (_ctx.controller.IsBusy) return;    // mid swing/cooldown -> hold the aim, wait
        _s.Attack.Tick(_ctx);                  // fire along FacingDir
        _state = State.Idle;                   // then re-decide in Idle (chase back in if it dashed out of range)
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

    // Hit from anywhere -> fight back. Passive monsters enter combat only through this. Route into Idle so the
    // decide hub picks it up (face + choose) next frame.
    void OnDamaged(object source)
    {
        var attacker = (source as Component)?.GetComponentInParent<IDamageable>();
        _ctx.target = attacker ?? _ctx.FindHostile(_ctx.config != null ? _ctx.config.aggroRadius : 0f);
        if (_ctx.target != null && _state == State.Forget) _state = State.Idle;
    }
}
