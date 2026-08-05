// What a node DOES when it is owned.
//
// [SerializeReference], exactly like EnemyBrainConfig's behaviours: the slot both picks the kind of effect
// and holds that kind's own settings, on one screen. A new kind of upgrade is ONE [Serializable] class and
// it appears in the dropdown by itself — nothing to register, no factory to extend, no enum to keep in step.
//
// NOT EVERY NODE IS A BUFF. That is the whole reason this is an interface rather than a list of stat changes
// on the node: unlocking a skill, opening a slot, changing a rule are all nodes too, and they arrive as new
// classes without the tree, the save or the popup learning anything.
public interface IUpgradeEffect
{
    void Apply(UpgradeContext context);

    // The line the player reads before buying: "+10% attack". Written by the EFFECT, because the effect is the
    // only thing that knows what it does — a description assembled by the popup would need a branch per kind
    // of upgrade, which is the thing [SerializeReference] is here to avoid. It is also the only wording that
    // cannot lie: it is built from the same numbers Apply uses.
    string Describe();
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

    public UpgradeContext(MainCharStats stats, object source)
    {
        Stats = stats;
        Source = source;
    }
}
