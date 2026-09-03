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
    public IStat Recovery => _byId[StatId.Recovery];
    public IStat MaxHp => _byId[StatId.MaxHp];
    public IStat CritPoints => _byId[StatId.CritPoints];
    public IStat CritDamage => _byId[StatId.CritDamage];
    public IStat MaxHunger => _byId[StatId.MaxHunger];
    public IStat HungerDrain => _byId[StatId.HungerDrain];
    public IStat Regen => _byId[StatId.Regen];
    public IStat Mass => _byId[StatId.Mass];
    public IStat PickupRadius => _byId[StatId.PickupRadius];
    public IStat Vision => _byId[StatId.Vision];
    public IStat Skill1Haste => _byId[StatId.Skill1Haste];
    public IStat Skill2Haste => _byId[StatId.Skill2Haste];

    public MainCharStats(MainCharStatsConfig config)
    {
        _byId[StatId.MoveSpeed] = new Stat(config.moveSpeed);
        _byId[StatId.AttackSpeed] = new Stat(config.attackSpeed);
        _byId[StatId.AttackPower] = new Stat(config.attackPower);
        _byId[StatId.Recovery] = new Stat(config.recovery);
        _byId[StatId.MaxHp] = new Stat(config.maxHp);
        _byId[StatId.CritPoints] = new Stat(config.critPoints);
        _byId[StatId.CritDamage] = new Stat(config.critDamage);
        _byId[StatId.MaxHunger] = new Stat(config.maxHunger);
        _byId[StatId.HungerDrain] = new Stat(config.hungerDrain);
        _byId[StatId.Regen] = new Stat(config.wellFedHealPercent);   // percent per second, as authored
        _byId[StatId.Mass] = new Stat(config.mass);
        _byId[StatId.PickupRadius] = new Stat(config.pickupRadius);
        // ALWAYS 1, and not a config field. What one character sees compared to another is already said by
        // the spotlight drawn on their prefab, so a number here would be a second place to say it — free to
        // disagree with the art, and with only one value that is ever right. This stat exists to be MODIFIED:
        // 1 is "the light this character was drawn with", and gear, upgrades and buffs move it from there.
        _byId[StatId.Vision] = new Stat(1f);

        // ALWAYS 0, for the same reason Vision is always 1: the number a character starts with is already
        // said elsewhere. How long a skill takes to come back is the SKILL's number, authored on the prefab
        // that carries it (see CharacterSkill), so what is left here is only what upgrades and gear add on
        // top. Zero haste is "exactly the cooldown this skill was built with".
        _byId[StatId.Skill1Haste] = new Stat(0f);
        _byId[StatId.Skill2Haste] = new Stat(0f);
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

    // Across EVERY stat, because the thing this exists for — the tree dropping all of its modifiers and adding
    // them back — is a rebuild of the whole set and not of one number. See Stat.BeginBatch for what goes wrong
    // when the outside watches that happen step by step. Always in a try/finally: a throw halfway through would
    // otherwise leave every stat silent for good.
    public void BeginBatch()
    {
        foreach (var stat in _byId.Values) stat.BeginBatch();
    }

    public void EndBatch()
    {
        foreach (var stat in _byId.Values) stat.EndBatch();
    }
}
