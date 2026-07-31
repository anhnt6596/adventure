public class MainCharStats : ICharacterStats
{
    public Stat MoveSpeed { get; }
    public Stat AttackSpeed { get; }
    public Stat AttackPower { get; }
    public Stat MaxHunger { get; }
    public Stat HungerDrain { get; }
    public float AttackCooldown { get; }
    public float Mass { get; }
    public float PickupRadius { get; }

    public MainCharStats(MainCharStatsConfig config)
    {
        MoveSpeed = new Stat(config.moveSpeed);
        AttackSpeed = new Stat(config.attackSpeed);
        AttackPower = new Stat(config.attackPower);
        MaxHunger = new Stat(config.maxHunger);
        HungerDrain = new Stat(config.hungerDrain);
        AttackCooldown = config.attackCooldown;
        Mass = config.mass;
        PickupRadius = config.pickupRadius;
    }
}
