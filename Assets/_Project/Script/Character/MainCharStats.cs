using VContainer;

public class MainCharStats : ICharacterStats
{
    public Stat MoveSpeed { get; }
    public Stat AttackSpeed { get; }

    [Inject]
    public MainCharStats(MainCharStatsConfig config)
    {
        MoveSpeed = new Stat(config.moveSpeed);
        AttackSpeed = new Stat(config.attackSpeed);
    }
}
