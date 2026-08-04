using System;
using System.Collections.Generic;
using UnityEngine;

// The plain "this node makes a number better" upgrade, and it covers most of a tree on its own.
//
// A LIST, NOT ONE BUFF. Plenty of upgrades are one idea expressed in two numbers — a heavier build is more
// health AND slower, a lighter one the reverse — and that is one node, not two. Splitting them would mean
// two nodes the player has to buy together for either to make sense.
//
// This is also why there is no class per stat: which stat is data, so a hundred stat upgrades are a hundred
// rows rather than a hundred classes.
[Serializable]
public class StatBuffEffect : IUpgradeEffect
{
    [SerializeField] List<StatBuff> buffs = new List<StatBuff>();

    public IReadOnlyList<StatBuff> Buffs => buffs;

    public void Apply(UpgradeContext context)
    {
        if (buffs == null || context.Stats == null) return;

        foreach (var buff in buffs)
            context.Stats.Modifiable(buff.stat)?.Add(new StatModifier(buff.amount, buff.kind, context.Source));
    }
}

[Serializable]
public struct StatBuff
{
    [Tooltip("Which stat. Picked from StatId in the inspector rather than typed — see the tree editor.")]
    public string stat;

    [Tooltip("Add is summed with other Adds. Mul and FinalMul are MULTIPLIED, so the neutral value is 1 and " +
             "1.5 means x1.5 — typing 0.5 halves the stat rather than raising it by half.")]
    public StatModKind kind;

    public float amount;
}
