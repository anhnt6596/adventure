public interface ICharacterStats
{
    Stat MoveSpeed { get; }
    Stat AttackSpeed { get; }
    Stat AttackPower { get; }

    // The stomach and how fast it empties. Stats rather than plain config numbers because both are meant
    // to grow — a level, a bag, a trait that makes you eat less. Nothing modifies them yet; putting them
    // here now is what stops that day being a refactor.
    Stat MaxHunger { get; }
    Stat HungerDrain { get; }

    float AttackCooldown { get; }
    float Mass { get; }
    float PickupRadius { get; }
}
