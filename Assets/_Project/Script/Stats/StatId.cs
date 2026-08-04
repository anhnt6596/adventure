// The names a stat can be asked for by.
//
// STRINGS RATHER THAN AN ENUM, on purpose: a buff is authored as data — a node in an upgrade tree says
// "Add 20 to MaxHp" — and data that names an enum member is data that breaks when somebody reorders the
// enum. A name survives being moved, and a new stat is one entry here rather than an enum plus every switch
// that has to learn about it.
//
// THE COST IS TYPOS, and it is paid for in the editor rather than accepted: All is what the inspector turns
// into a dropdown, so nothing authored is ever typed by hand. Anything that gets through anyway resolves to
// null and says so once, rather than silently buffing nothing.
public static class StatId
{
    public const string MoveSpeed = "MoveSpeed";
    public const string AttackSpeed = "AttackSpeed";
    public const string AttackPower = "AttackPower";
    public const string AttackCooldown = "AttackCooldown";
    public const string MaxHp = "MaxHp";
    public const string MaxHunger = "MaxHunger";
    public const string HungerDrain = "HungerDrain";
    public const string Mass = "Mass";
    public const string PickupRadius = "PickupRadius";

    public static readonly string[] All =
    {
        MoveSpeed, AttackSpeed, AttackPower, AttackCooldown,
        MaxHp, MaxHunger, HungerDrain, Mass, PickupRadius,
    };
}
