public interface ICharacterStats
{
    Stat MoveSpeed { get; }
    Stat AttackSpeed { get; }
    Stat AttackPower { get; }
    float AttackDuration { get; }
    float Mass { get; }
}
