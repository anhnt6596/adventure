using System;
using UnityEngine;

// Opens one of the character's skills. The first node that is not a number, and the shape every later one of
// its kind should copy: it writes a FACT into the context and changes nothing itself.
//
// BY THE SKILL'S KEY, the same name its icon hangs on — one name for one skill, so a node, a picture and a
// component all answer to it. Not a reference to the component, because the component lives on a prefab that
// does not exist while the tree is being read, and is thrown away and rebuilt on every spawn.
//
// The cost of naming by string, stated plainly: a key that matches nothing is a node that silently does
// nothing. The tree editor cannot check it either — skill keys live on prefabs and the tree has no idea which
// character will be carrying which. Type it once, next to the one on the skill.
[Serializable]
public class UnlockSkillEffect : IUpgradeEffect
{
    [Tooltip("The Key of the skill this opens — the same string typed on the CharacterSkill component, and " +
             "the same one its icon is named after.")]
    [SerializeField] string skill = "";

    [Tooltip("What the node says it does. The other effects build their line from the numbers they carry; " +
             "this one has no number, and 'Unlocks dash' is a sentence rather than a value.")]
    [SerializeField] string description = "";

    public void Apply(UpgradeContext context) => context.Unlock(skill);

    // The rank is ignored: opening a thing twice opens it once. A node carrying this has no reason to have
    // more than one rank, and if somebody gives it more the wording stays honest rather than claiming a
    // second unlock.
    public string Describe(int rank)
        => !string.IsNullOrWhiteSpace(description) ? description
         : !string.IsNullOrWhiteSpace(skill) ? $"Unlocks {skill}"
         : "";
}
