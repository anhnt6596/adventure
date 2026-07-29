public class MainCharStats : ICharacterStats
{
    public Stat MoveSpeed { get; }
    public Stat AttackSpeed { get; }
    public Stat AttackPower { get; }
    public float AttackDuration { get; }
    public float AttackCooldown { get; }
    public float Mass { get; }
    public float PickupRadius { get; }

    public MainCharStats(MainCharStatsConfig config)
    {
        MoveSpeed = new Stat(config.moveSpeed);
        AttackSpeed = new Stat(config.attackSpeed);
        AttackPower = new Stat(config.attackPower);
        AttackDuration = config.attackDuration;
        AttackCooldown = config.attackCooldown;
        Mass = config.mass;
        PickupRadius = config.pickupRadius;
    }
}
