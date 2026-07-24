using UnityEngine;
using VContainer;

// The body + stats of an enemy: stats come from its EnemyConfig (injected by EnemySpawner, the same way MC
// gets its stats — no serialized ref on the prefab), team is 2. It exposes Move()/Attack() from
// UnitController but decides nothing itself — a separate tactic component reads the config and drives it.
public class EnemyController : UnitController
{
    EnemyConfig config;   // injected at spawn via EnemySpawner's per-kind scope

    public EnemyConfig Config => config;   // tactics read stats they need (damage, ...) off this

    [Inject]
    public void Construct(EnemyConfig config) => this.config = config;

    public override int Team => 2;   // enemy

    // Null-safe so a missing config leaves the enemy inert (Start logs it) instead of crashing the base loop.
    protected override float MoveSpeed => config != null ? config.moveSpeed : 0f;
    protected override float AttackSpeed => config != null ? config.attackSpeed : 1f;
    protected override float AttackDuration => 0.4f;   // swing window; a constant for now (move to config if it needs tuning)
    protected override float Mass => 1f;

    protected override void Start()
    {
        base.Start();
        if (config == null)
            Debug.LogError($"[{nameof(EnemyController)}] no EnemyConfig injected — spawn it through EnemySpawner, which binds the config.", this);
    }
}
