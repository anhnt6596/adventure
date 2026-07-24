using VContainer;

// The player unit. Its stats come from the injected ICharacterStats (the active main char's config); the
// movement/attack control lives in UnitController. Everything player-specific (input, interaction, camera,
// PlayerSystem) references this concrete type — an enemy is a different UnitController subclass.
public class MC : UnitController
{
    ICharacterStats _stats;

    [Inject]
    public void Construct(ICharacterStats stats) => _stats = stats;

    protected override float MoveSpeed => _stats.MoveSpeed.Value;
    protected override float AttackSpeed => _stats.AttackSpeed.Value;
    protected override float AttackDuration => _stats.AttackDuration;
    protected override float Mass => _stats.Mass;
}
