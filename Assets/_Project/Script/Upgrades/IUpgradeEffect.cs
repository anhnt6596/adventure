using System.Collections.Generic;

// What a node DOES when it is owned.
//
// [SerializeReference], exactly like EnemyBrainConfig's behaviours: the slot both picks the kind of effect
// and holds that kind's own settings, on one screen. A new kind of upgrade is ONE [Serializable] class and
// it appears in the dropdown by itself — nothing to register, no factory to extend, no enum to keep in step.
//
// NOT EVERY NODE IS A BUFF. That is the whole reason this is an interface rather than a list of stat changes
// on the node: unlocking a skill, opening a slot, changing a rule are all nodes too, and they arrive as new
// classes without the tree, the save or the popup learning anything.
//
// EVERY EFFECT MUST BE SAFE TO APPLY AGAIN. The whole tree is taken off and put back on every purchase (see
// PlayerSystem.ApplyUpgrades), so Apply is called many times for one bought node and has to land in the same
// place each time. Everything so far is: a stat modifier is tagged and removed first, an unlock writes a name
// into a set that was just emptied. The day an effect cannot be — one that hands over an item, say — that is
// the day the tree stops re-applying and starts tracking what it has already done.
public interface IUpgradeEffect
{
    void Apply(UpgradeContext context);

    // The line the player reads before buying: "+10% attack". Written by the EFFECT, because the effect is the
    // only thing that knows what it does — a description assembled by the popup would need a branch per kind
    // of upgrade, which is the thing [SerializeReference] is here to avoid. It is also the only wording that
    // cannot lie: it is built from the same numbers Apply uses.
    //
    // AT A GIVEN RANK, because the tooltip asks the same effect twice: once at 1 for what a point buys, and
    // once at the rank owned for what has been bought so far. The effect scales itself rather than the caller
    // multiplying a string — only the effect knows which of its numbers are amounts and which are shares, and
    // an unlock does not scale at all.
    string Describe(int rank);
}

// What an effect is allowed to touch. A struct passed by value, so an effect cannot hold onto it past the
// call and start modifying a character that has since been replaced.
//
// It exists instead of passing MainCharStats directly so that the day an effect needs something else — a
// skill set, an inventory, a flag on the save — the signature above does not change and neither does
// anything that already implements it.
public readonly struct UpgradeContext
{
    public readonly MainCharStats Stats;

    // Everything an upgrade adds is tagged with this, so the whole tree comes off in one call when it is
    // re-applied or reset. See PlayerSystem.ApplyUpgrades.
    public readonly object Source;

    // Skills a node has opened, by CharacterSkill.Key. NOT applied to anything here — an effect writes the
    // name in, and PlayerSystem turns the finished set into which components on the body are live.
    //
    // A SET REBUILT EACH PASS, not a flag on the skill, for the same reason UpgradeSystem stores the bought
    // nodes and derives everything else: what is open is a FUNCTION of what has been bought, so a respec
    // closes it again without anything having to remember to. It also survives the body being thrown away and
    // rebuilt, which happens on every spawn.
    public readonly HashSet<string> UnlockedSkills;

    public UpgradeContext(MainCharStats stats, object source,
                          HashSet<string> unlockedSkills, List<SkillModifier> skillBuffs)
    {
        Stats = stats;
        Source = source;
        UnlockedSkills = unlockedSkills;
        SkillBuffs = skillBuffs;
    }

    // Modifiers bound for a skill's own numbers, in the order the nodes were read. Collected rather than
    // applied for the same reason as the unlocks: the body may not exist yet, and the same list has to be
    // usable again the moment one does.
    public readonly List<SkillModifier> SkillBuffs;

    public void Unlock(string skillKey)
    {
        if (!string.IsNullOrEmpty(skillKey)) UnlockedSkills?.Add(skillKey);
    }

    public void BuffSkill(string skillKey, string stat, StatModKind kind, float amount)
    {
        if (string.IsNullOrEmpty(skillKey) || string.IsNullOrEmpty(stat)) return;
        SkillBuffs?.Add(new SkillModifier(skillKey, stat, kind, amount));
    }
}

// One pending change to one skill's number, still naming its target rather than pointing at it — the thing it
// is for may not have been built yet. Resolved in PlayerSystem, which is where the live body is known.
public readonly struct SkillModifier
{
    public readonly string Skill;   // CharacterSkill.Key
    public readonly string Stat;    // the tunable's name, as that skill declares it
    public readonly StatModKind Kind;
    public readonly float Amount;

    public SkillModifier(string skill, string stat, StatModKind kind, float amount)
    {
        Skill = skill;
        Stat = stat;
        Kind = kind;
        Amount = amount;
    }
}
