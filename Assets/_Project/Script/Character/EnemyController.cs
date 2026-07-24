using UnityEngine;

// The body + stats of an enemy: stats come from its EnemyConfig, team is 2. It exposes Move()/Attack() from
// UnitController but decides nothing itself — a separate tactic component (per enemy kind) reads the config
// and drives it, the way MCInput drives MC.
public class EnemyController : UnitController
{
    [SerializeField] EnemyConfig config;   // drag the kind's config (e.g. Mewfrog); a factory will set it later

    public EnemyConfig Config => config;   // tactics read stats they need (attackRange, damage, ...) off this

    // A spawner binds the kind's config in at spawn (overrides the serialized drag default).
    public void Configure(EnemyConfig cfg) => config = cfg;

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
            Debug.LogError($"[{nameof(EnemyController)}] no EnemyConfig — drag the kind's config (e.g. Mewfrog).", this);
    }
}
