public interface ICharacterStats
{
    Stat MoveSpeed { get; }
    Stat AttackSpeed { get; }
    Stat AttackPower { get; }
    float AttackCooldown { get; }
    float Mass { get; }
    float PickupRadius { get; }
}
