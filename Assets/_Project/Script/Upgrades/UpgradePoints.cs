using System;
using System.Collections.Generic;
using Core.Save;
using UnityEngine;
using VContainer;

// What the upgrade tree is bought with. A currency of its own, per character, saved.
//
// IT USED TO BE THE LEVEL, and that stopped being true rather than stopped being convenient. A level was a
// point because levelling was the only thing that happened to a character; now progress is made by clearing
// arenas, and tying the tree to a number that climbs for a different reason would mean every change to how
// experience works is silently a change to how strong the tree makes you. Two things that grow for two
// reasons need two numbers.
//
// EARNED, NOT DERIVED. Nothing here computes a balance from levels, kills or anything else: points are handed
// over by whatever decided to hand them over, and spent by the tree. That is what lets an arena pay for a run
// without the tree having to know arenas exist.
//
// THE BALANCE IS WHAT IS STORED, and what has been spent is worked out by UpgradeSystem off the ranks — so a
// reprice or a deleted node changes what a character can afford without anybody migrating a save. Same rule
// UpgradeSystem already keeps by storing rank counts and never "points remaining".
//
// PER CHARACTER, like the ranks they buy. A shared pool would let time spent with one character be spent out
// of another's tree, which is the thing UpgradeTreeConfig says points exist to prevent.
public class UpgradePoints : ISavable
{
    readonly Dictionary<string, int> _earned = new Dictionary<string, int>();
    readonly SaveService _save;

    public string SaveKey => "upgradepoints";

    // Fired when a character's total moves. UpgradeSystem does not need it — it reads the total when asked —
    // but anything showing a number does.
    public event Action<string> Changed;

    [Inject]
    public UpgradePoints(SaveService save)
    {
        _save = save;
        _save.Register(this);   // loads _earned
    }

    // Everything this character has ever been given. NOT what is left: how much of it is already committed is
    // a question about the ranks, and UpgradeSystem is the one holding those.
    public int Earned(string characterId)
        => !string.IsNullOrEmpty(characterId) && _earned.TryGetValue(characterId, out int n) ? n : 0;

    public void Award(string characterId, int amount)
    {
        if (string.IsNullOrEmpty(characterId) || amount <= 0) return;

        _earned[characterId] = Earned(characterId) + amount;
        _save.Save(SaveKey);
        Changed?.Invoke(characterId);
    }

#if UNITY_EDITOR
    // EDITOR ONLY, and it can go negative-looking the way MaxAll can: for walking a tree up and down while
    // judging it, without earning the points first.
    public void SetEarned(string characterId, int total)
    {
        if (string.IsNullOrEmpty(characterId)) return;

        _earned[characterId] = Mathf.Max(0, total);
        _save.Save(SaveKey);
        Changed?.Invoke(characterId);
    }
#endif

    public void Save(SaveBag bag) => bag.Set("Earned", new Dictionary<string, int>(_earned));

    public void Load(SaveBag bag)
    {
        _earned.Clear();
        foreach (var kv in bag.Get("Earned", new Dictionary<string, int>()))
            if (!string.IsNullOrEmpty(kv.Key)) _earned[kv.Key] = Mathf.Max(0, kv.Value);
    }
}
