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

    // A line per buff, for the same reason the list exists: a node that is two numbers has to say both, or it
    // is selling the good half and hiding the other.
    public string Describe(int rank)
    {
        if (buffs == null || buffs.Count == 0) return "";

        // A rank is the same buff again, so a rank's worth is the amount times the count — the same
        // arithmetic PlayerSystem does by applying the effect that many times.
        int times = Mathf.Max(1, rank);

        var lines = new List<string>();
        foreach (var buff in buffs)
            if (!string.IsNullOrEmpty(buff.stat)) lines.Add(Describe(buff, times));

        return string.Join("\n", lines);
    }

    // Add is the number itself, a share is the number as a percentage — the same distinction the stat makes
    // when it applies them, so the wording cannot drift from the arithmetic. See StatModifier.
    //
    // The unit rides along on Add only, because on Add the number is in the stat's own units and some stats are
    // not a bare count (Regen is a percent per second). On Mul the percent printed here is already the share,
    // and a stat unit on top of it would read as a second, different percent. See StatId.Unit.
    static string Describe(StatBuff buff, int times)
        => buff.kind == StatModKind.Add
            ? $"{Signed(buff.amount * times)}{StatId.Unit(buff.stat)} {StatId.Display(buff.stat)}"
            : $"{Signed(buff.amount * times * 100f)}% {StatId.Display(buff.stat)}";

    static string Signed(float value) => value >= 0f ? $"+{value:0.##}" : value.ToString("0.##");
}

[Serializable]
public struct StatBuff
{
    [Tooltip("Which stat. Picked from StatId in the inspector rather than typed — see the tree editor.")]
    public string stat;

    [Tooltip("Every kind is what it ADDS, so 0 always means 'no change'. Add is flat; Mul and FinalMul are a " +
             "share — 0.1 is +10%, -0.5 takes half away. They stack by adding up: two 0.1s make +20%, not +21%.")]
    public StatModKind kind;

    public float amount;
}
