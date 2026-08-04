// Everything about a character that a buff can move. All of it is IStat now, including the three that used
// to be plain floats: a number that cannot be modified is a number no upgrade can ever touch, and deciding
// which ones those are up front is deciding which upgrades can never be designed.
//
// READ-ONLY BY DESIGN. Every member here hands back IStat, so holding this lets you know what the character
// is and be told when it changes, but not change it. Whoever owns the stats holds MainCharStats and is the
// only thing that can.
//
// ENEMIES DO NOT HAVE THIS. Their numbers come straight off EnemyConfig, because they are spawned in
// numbers, live briefly, and nothing buffs them yet — a modifier stack each would be allocation for a
// feature that does not exist. The day a player debuff needs to land on one, Damageable already takes its
// maximum through a seam that either side can fill (see Unit.MaxHpStat).
public interface ICharacterStats
{
    IStat MoveSpeed { get; }
    IStat AttackSpeed { get; }
    IStat AttackPower { get; }
    IStat AttackCooldown { get; }
    IStat MaxHp { get; }
    IStat MaxHunger { get; }
    IStat HungerDrain { get; }
    IStat Mass { get; }
    IStat PickupRadius { get; }

    // By name, for anything authored as data. Null for a name that is not a stat — see StatId.
    IStat Get(string id);
}
