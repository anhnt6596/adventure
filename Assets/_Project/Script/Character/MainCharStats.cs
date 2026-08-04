using System.Collections.Generic;
using UnityEngine;

// The main character's live numbers, built from its config and then pushed around by whatever is on it.
//
// ONE PER BODY, BUILT AT SPAWN. PlayerSystem makes a fresh set every time it spawns, so switching character
// throws the old set away entirely — which is why nothing ever has to be un-applied when you swap. It is
// also why a timed buff dies with the body it was on, and that is the right answer: a buff cast on one
// character has no business following the next one out of the door.
//
// THE DICTIONARY IS THE ONE PLACE A NAME BECOMES A STAT. Both the typed properties and Get read the same
// entries, so there is no second list to fall out of step — adding a stat is one line in the constructor
// and one in StatId.
public class MainCharStats : ICharacterStats
{
    readonly Dictionary<string, Stat> _byId = new Dictionary<string, Stat>();

    public IStat MoveSpeed => _byId[StatId.MoveSpeed];
    public IStat AttackSpeed => _byId[StatId.AttackSpeed];
    public IStat AttackPower => _byId[StatId.AttackPower];
    public IStat AttackCooldown => _byId[StatId.AttackCooldown];
    public IStat MaxHp => _byId[StatId.MaxHp];
    public IStat MaxHunger => _byId[StatId.MaxHunger];
    public IStat HungerDrain => _byId[StatId.HungerDrain];
    public IStat Mass => _byId[StatId.Mass];
    public IStat PickupRadius => _byId[StatId.PickupRadius];

    public MainCharStats(MainCharStatsConfig config)
    {
        _byId[StatId.MoveSpeed] = new Stat(config.moveSpeed);
        _byId[StatId.AttackSpeed] = new Stat(config.attackSpeed);
        _byId[StatId.AttackPower] = new Stat(config.attackPower);
        _byId[StatId.AttackCooldown] = new Stat(config.attackCooldown);
        _byId[StatId.MaxHp] = new Stat(config.maxHp);
        _byId[StatId.MaxHunger] = new Stat(config.maxHunger);
        _byId[StatId.HungerDrain] = new Stat(config.hungerDrain);
        _byId[StatId.Mass] = new Stat(config.mass);
        _byId[StatId.PickupRadius] = new Stat(config.pickupRadius);
    }

    public IStat Get(string id) => Modifiable(id);

    // The write side. Deliberately NOT on ICharacterStats: everything that reads a stat takes the interface
    // and cannot reach this, so the list of things able to buff a character stays short enough to name.
    public Stat Modifiable(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (_byId.TryGetValue(id, out var stat)) return stat;

        Debug.LogWarning($"[{nameof(MainCharStats)}] no stat called '{id}' — whatever asked for it has no " +
                         "effect. Check it against StatId.");
        return null;
    }

    // Takes off everything one source put on, across every stat. The upgrade tree re-applies itself this
    // way — drop all of mine, add all of mine — which is one code path for buying, resetting and respawning
    // instead of three that have to agree.
    public void RemoveBySource(object source)
    {
        if (source == null) return;
        foreach (var stat in _byId.Values) stat.RemoveBySource(source);
    }
}
