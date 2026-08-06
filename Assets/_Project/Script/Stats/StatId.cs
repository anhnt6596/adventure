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

    // PERCENT of max HP regained per second while above the well-fed line — 0.75 means 0.75%/s, the same
    // units the config is authored in. Where the line SITS is not this: that is a rule of the mechanic and
    // the same for everybody, so it stays a plain number on the config. How fast you mend once you are over
    // it is a property of the character, which is exactly what a stat is for.
    public const string Regen = "Regen";
    public const string Mass = "Mass";
    public const string PickupRadius = "PickupRadius";

    // How much of the night the character is shown, as a multiple of the light their prefab is drawn with —
    // 1 is exactly as authored. NOT a radius in world units on purpose: the light is a glow that fades out,
    // so the distance it "reaches" is a matter of taste that was already settled by eye when somebody set the
    // sprite's scale. A number in metres here would be that same judgement written down a second time.
    //
    // NOT the same thing as PickupRadius, and Docs/DESIGN.md says so outright: "Two different radii — vision
    // and pickup. Don't let them merge in code or UI." One decides how much of the night is shown to you, the
    // other how far loot walks to you, and a night where those are one number is a night where a magnet
    // upgrade lights the map.
    public const string Vision = "Vision";

    public static readonly string[] All =
    {
        MoveSpeed, AttackSpeed, AttackPower, AttackCooldown,
        MaxHp, MaxHunger, HungerDrain, Regen, Mass, PickupRadius, Vision,
    };

    // What a stat is CALLED where a player can see it. Beside the ids because the two have to grow together:
    // a stat that gains an id without a name shows up in the game spelled the way a programmer typed it.
    //
    // English in code is a placeholder, the same one the upgrade labels are — when a string table exists this
    // is the one place that has to change.
    public static string Display(string id) => id switch
    {
        MoveSpeed => "move speed",
        AttackSpeed => "attack speed",
        AttackPower => "attack",
        AttackCooldown => "attack cooldown",
        MaxHp => "HP",
        MaxHunger => "max fullness",
        HungerDrain => "hunger drain",
        // WITH THE CONDITION ATTACHED, not just "regen": this stat does nothing below the well-fed line, and a
        // node that promises "+20% regen" sells healing the player will not see until they eat. "well fed"
        // rather than "full" because the line sits at 75% fullness — see MainCharStatsConfig.wellFed.
        Regen => "regen when well fed",
        Mass => "mass",
        PickupRadius => "pickup radius",
        Vision => "vision",
        _ => id,
    };

    // What the stat's OWN number is measured in, for a flat change to it. Most stats are a bare count — 20 HP,
    // 3 mass — and say nothing here. Regen is not: it is a percent of max HP, so printing the bare number
    // leaves the player reading "+0.5" with nothing to say what of.
    //
    // JUST THE PERCENT, no "/s", even though the stat really is a rate: the sign is there to give the number a
    // scale, and "+0.5%/s" spends three characters of punctuation on a detail no player is doing arithmetic
    // with. If a tooltip ever has to teach the mechanic, that is a sentence about regen, not a suffix.
    //
    // FOR Add ONLY, and StatBuffEffect is careful about it: on Mul the percent already in the line is the SHARE
    // of the stat ("+20% regen"), so appending this too would print two different percents in one line, one of
    // which is not what was bought.
    public static string Unit(string id) => id switch
    {
        Regen => "%",
        _ => "",
    };
}
