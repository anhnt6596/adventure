using UnityEngine;
using VContainer;

// The body + stats of an enemy: stats come from its EnemyConfig (injected by EnemySpawner, the same way MC
// gets its stats — no serialized ref on the prefab), team is 2. It exposes Move()/Attack() from
// DynamicUnit but decides nothing itself — a separate tactic component reads the config and drives it.
public class EnemyController : DynamicUnit
{
    EnemyConfig config;   // injected at spawn via EnemySpawner's per-kind scope

    public EnemyConfig Config => config;   // tactics read stats they need (damage, ...) off this

    [Inject]
    public void Construct(EnemyConfig config) => this.config = config;

    // One team per KIND, off the config — see EnemyConfig.team. Falls back to the generic monster team only so
    // a config-less enemy is still hostile to the player rather than silently universal.
    public override int Team => config != null ? config.team : Teams.FirstMonster;
    public override IDamageableConfig DamageableConfig => config;   // Damageable reads HP off this

    // Null-safe so a missing config leaves the enemy inert (Start logs it) instead of crashing the base loop.
    protected override float MoveSpeed => config != null ? config.moveSpeed : 0f;
    protected override float AttackSpeed => config != null ? config.attackSpeed : 1f;
    public override float Recovery => config != null ? config.recovery : 0f;
    protected override float Mass => config != null ? config.mass : 1f;
    public override float AttackPower => config != null ? config.attackDamage : 0f;

    protected override void Start()
    {
        base.Start();
        if (config == null)
            Debug.LogError($"[{nameof(EnemyController)}] no EnemyConfig injected — spawn it through EnemySpawner, which binds the config.", this);
    }
}
