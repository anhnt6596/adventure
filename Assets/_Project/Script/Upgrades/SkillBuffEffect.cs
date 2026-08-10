using System;
using System.Collections.Generic;
using UnityEngine;

// Moves one of a SKILL's own numbers — a dash's distance, a shout's radius — rather than one of the
// character's.
//
// WHY NOT A CHARACTER STAT. Those are a fixed list every character carries, so a dash distance living there
// would be a number on every character that has no dash, and the list would grow with every skill ever
// written. Keying it by slot instead is worse, not better: the same DashSkill sits in slot one on one
// character and slot two on another, so a "Skill1Range" would mean a different thing per character and moving
// a skill between slots would quietly strand its upgrades.
//
// So a buff is addressed to (skill key, tunable name) — the same Key the skill's icon and its unlock node use.
// See CharacterSkill.
//
// IT APPLIES NOTHING ITSELF, like UnlockSkillEffect: the tree is read at times when the body does not exist,
// so this writes the intent into the context and PlayerSystem lands it on whatever is standing.
[Serializable]
public class SkillBuffEffect : IUpgradeEffect
{
    [Tooltip("The Key of the skill this changes — the same string typed on the CharacterSkill component.")]
    [SerializeField] string skill = "";

    [SerializeField] List<SkillBuff> buffs = new List<SkillBuff>();

    public void Apply(UpgradeContext context)
    {
        if (buffs == null || string.IsNullOrEmpty(skill)) return;

        foreach (var buff in buffs)
            if (!string.IsNullOrEmpty(buff.stat))
                context.BuffSkill(skill, buff.stat, buff.kind, buff.amount);
    }

    // A line per buff, the same shape StatBuffEffect prints. No unit and no display table: a skill's tunables
    // are its own names, and the tree has no way to know what a given skill calls things — so the node says
    // the name as typed. Ugly on purpose, like the placeholder labels elsewhere: it will not quietly ship.
    public string Describe(int rank)
    {
        if (buffs == null || buffs.Count == 0) return "";

        int times = Mathf.Max(1, rank);

        var lines = new List<string>();
        foreach (var buff in buffs)
            if (!string.IsNullOrEmpty(buff.stat))
                lines.Add(buff.kind == StatModKind.Add
                    ? $"{Signed(buff.amount * times)} {buff.stat}"
                    : $"{Signed(buff.amount * times * 100f)}% {buff.stat}");

        return string.Join("\n", lines);
    }

    static string Signed(float value) => value >= 0f ? $"+{value:0.##}" : value.ToString("0.##");
}

// Same three fields StatBuff has, and deliberately NOT the same type: that one's stat is picked from StatId in
// a dropdown, and this one's cannot be — which skill will be carrying this tunable is not knowable from the
// tree. Sharing the type would mean sharing the drawer, and the drawer would offer a list of character stats
// that are all wrong here.
[Serializable]
public struct SkillBuff
{
    [Tooltip("The tunable's name, as the skill declares it — DashSkill.Distance is \"distance\". Typed by " +
             "hand: the tree does not know which character carries which skill, so nothing can offer a list. " +
             "A name the skill does not have is reported once at runtime rather than failing silently.")]
    public string stat;

    [Tooltip("Add is flat, in the tunable's own units. Mul and FinalMul are a share — 0.1 is +10%.")]
    public StatModKind kind;

    public float amount;
}
