using VContainer;

// The player unit. Its stats come from the injected ICharacterStats (the active main char's config); the
// movement/attack control lives in DynamicUnit. Everything player-specific (input, interaction, camera,
// PlayerSystem) references this concrete type — an enemy is a different DynamicUnit subclass.
public class MCController : DynamicUnit
{
    ICharacterStats _stats;
    IDamageableConfig _dmg;

    public override int Team => Teams.Player;
    public override IDamageableConfig DamageableConfig => _dmg; // HP from MainCharStatsConfig

    // The live set this body was built with, for anything that wants to SHOW the numbers — the read side
    // only, so handing it out is not handing out permission to buff (see ICharacterStats). Bound like the
    // rest of the HUD's sources are: off the body, so a switch or a respawn re-points it by itself.
    public ICharacterStats Stats => _stats;

    // The main character's maximum HP is a live stat, so upgrades and buffs can move it. Damageable takes
    // this in preference to the config's flat number — see Unit.MaxHpStat.
    public override IStat MaxHpStat => _stats?.MaxHp;

    [Inject]
    public void Construct(ICharacterStats stats, IDamageableConfig dmg)
    {
        _stats = stats;
        _dmg = dmg;
    }

    protected override float MoveSpeed => _stats.MoveSpeed.Value;
    protected override float AttackSpeed => _stats.AttackSpeed.Value;
    protected override float AttackCooldown => _stats.AttackCooldown.Value;
    protected override float Mass => _stats.Mass.Value;
    public override float AttackPower => _stats.AttackPower.Value;
}
