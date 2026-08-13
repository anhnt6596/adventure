using System;
using System.Collections.Generic;
using Core.Save;
using UnityEngine;
using VContainer;

// How far each character has taken each upgrade node, and the arithmetic of paying for it.
//
// A RANK COUNT IS THE ONLY STATE, and everything else is derived from it: what is unlocked comes from the
// ranks plus the tree's `requires`, and how many points are left comes from the ranks plus the character's
// level. Nothing here stores "unlocked" or "points remaining" — a stored copy would be a second answer, free
// to disagree with the first the moment a price or a link is edited in the inspector. Same rule PayGateSystem
// keeps by storing deposits and never a paid flag.
//
// A RANK IS THE SAME UPGRADE AGAIN, not a bigger one: every rank costs the same and is worth the same. That
// is what keeps this a counter rather than a table — with per-rank values there would be a list to author per
// node, a curve to tune, and a save that has to remember which rung a number came from.
//
// POINTS ARE LEVELS. A character has as many points as it has levels, so "left" is level minus what every
// rank has cost — nothing about the pool is stored, it is arithmetic over the counts. RESET is therefore only
// "forget the counts": there is nothing to hand anything back to, which is why a full respec is three lines
// rather than a refund ledger.
public class UpgradeSystem : ISavable
{
    readonly Dictionary<string, Dictionary<string, int>> _ranks = new Dictionary<string, Dictionary<string, int>>();
    readonly SaveService _save;
    readonly CharacterLevels _levels;

    public string SaveKey => "upgrades";
    public event Action Changed;

    [Inject]
    public UpgradeSystem(SaveService save, CharacterLevels levels)
    {
        _save = save;
        _levels = levels;
        _save.Register(this);   // loads _bought
    }

    // CLAMPED AGAINST THE NODE, not returned raw. Lowering a node's maxRank in the inspector must not leave a
    // save charging for ranks that no longer exist — the same reason the cost is summed off the tree.
    public int RankOf(string characterId, UpgradeNode node)
    {
        if (node == null || string.IsNullOrEmpty(characterId)) return 0;
        if (!_ranks.TryGetValue(characterId, out var byNode)) return 0;
        return byNode.TryGetValue(node.id, out int rank) ? Mathf.Clamp(rank, 0, node.MaxRank) : 0;
    }

    // Owned at all. What it is for is drawing the line INTO a node as taken; it is deliberately not what
    // opens the next one — see IsUnlocked.
    public bool IsBought(string characterId, UpgradeNode node) => RankOf(characterId, node) > 0;

    public bool IsMaxed(string characterId, UpgradeNode node)
        => node != null && RankOf(characterId, node) >= node.MaxRank;

    // ANY one of the nodes leading in opens this one, not all of them. Letting two branches converge is only
    // worth doing if arriving by either is enough — under AND the second route buys the player nothing except
    // a longer wait, and the shape stops meaning anything.
    //
    // FULLY taken, though. One rank of a five-rank node is a toe in the water, and if that opened the ring
    // behind it the cheapest play would always be one rank of everything — the tree would reward spreading
    // and never committing, which is the opposite of what branches are for.
    public bool IsUnlocked(string characterId, UpgradeTreeConfig tree, UpgradeNode node)
    {
        if (tree == null || node == null) return false;

        // No requirements = it hangs off the implicit centre, so it is open from the first level.
        if (node.requires == null || node.requires.Length == 0) return true;

        foreach (var requiredId in node.requires)
        {
            var required = tree.Find(requiredId);
            if (required != null && IsMaxed(characterId, required)) return true;
        }
        return false;
    }

    // Summed off the TREE rather than off the save, so a node deleted from the config stops charging for
    // itself and a reprice takes effect without anybody having to migrate a save.
    public int Spent(string characterId, UpgradeTreeConfig tree)
    {
        if (tree == null) return 0;

        int spent = 0;
        foreach (var node in tree.Nodes)
            if (node != null) spent += RankOf(characterId, node) * Mathf.Max(1, node.cost);
        return spent;
    }

    public int Available(string characterId, UpgradeTreeConfig tree)
        => _levels.Level(characterId) - Spent(characterId, tree);

    public bool CanBuy(string characterId, UpgradeTreeConfig tree, UpgradeNode node)
        => node != null
           && !IsMaxed(characterId, node)
           && IsUnlocked(characterId, tree, node)
           && Available(characterId, tree) >= Mathf.Max(1, node.cost);

    // One rank per call. The effect is not applied here: the whole tree is rebuilt onto the character by
    // PlayerSystem whenever this fires, which is what lets a rank simply be a number that gets counted twice
    // rather than an amount somebody has to add and remember.
    public bool Buy(string characterId, UpgradeTreeConfig tree, UpgradeNode node)
    {
        if (string.IsNullOrEmpty(characterId) || !CanBuy(characterId, tree, node)) return false;

        if (!_ranks.TryGetValue(characterId, out var byNode))
            _ranks[characterId] = byNode = new Dictionary<string, int>();

        byNode[node.id] = RankOf(characterId, node) + 1;
        _save.Save(SaveKey);
        Changed?.Invoke();
        return true;
    }

#if UNITY_EDITOR
    // EDITOR ONLY. One rank back off a node, the other direction of Buy — for walking a node up and down
    // while judging what a single rank is actually worth, which otherwise costs a Reset and a re-climb every
    // time. Requirements are not re-checked on the way down: anything past this node keeps the ranks it has
    // and simply reads as locked again, the same half-state MaxAll can leave behind.
    public bool Unbuy(string characterId, UpgradeNode node)
    {
        if (node == null || string.IsNullOrEmpty(characterId)) return false;

        int rank = RankOf(characterId, node);
        if (rank <= 0 || !_ranks.TryGetValue(characterId, out var byNode)) return false;

        byNode[node.id] = rank - 1;
        _save.Save(SaveKey);
        Changed?.Invoke();
        return true;
    }

    // EDITOR ONLY, AND IT DOES NOT PAY. Every node straight to its ceiling, price and requirements both
    // ignored — the point is to look at a finished tree without grinding the levels for it, and the nodes
    // worth looking at are usually the ones behind a branch nobody has opened.
    //
    // Available() will read negative afterwards, and that is left showing on purpose: the cheat should be
    // visible while it is on rather than hidden behind a number that has been quietly told to stop counting.
    // Reset is the way back, the same one a player has.
    public void MaxAll(string characterId, UpgradeTreeConfig tree)
    {
        if (string.IsNullOrEmpty(characterId) || tree == null) return;

        if (!_ranks.TryGetValue(characterId, out var byNode))
            _ranks[characterId] = byNode = new Dictionary<string, int>();

        bool changed = false;
        foreach (var node in tree.Nodes)   // hoisted by the caller's loop; Nodes flattens the inherited chain
        {
            if (node == null || string.IsNullOrEmpty(node.id)) continue;
            if (RankOf(characterId, node) >= node.MaxRank) continue;

            byNode[node.id] = node.MaxRank;
            changed = true;
        }

        if (!changed) return;   // already full — no save, and nothing redraws for a no-op
        _save.Save(SaveKey);
        Changed?.Invoke();
    }
#endif

    // Full respec. Points are levels, so nothing has to be given back: dropping the counts is what makes them
    // spendable again. The character keeps its level, and therefore its total.
    public void Reset(string characterId)
    {
        if (string.IsNullOrEmpty(characterId)) return;
        if (!_ranks.TryGetValue(characterId, out var byNode) || byNode.Count == 0) return;

        byNode.Clear();
        _save.Save(SaveKey);
        Changed?.Invoke();
    }

    public void Save(SaveBag bag)
    {
        var data = new Dictionary<string, Dictionary<string, int>>();
        foreach (var kv in _ranks)
        {
            var owned = new Dictionary<string, int>();
            foreach (var node in kv.Value)
                if (node.Value > 0) owned[node.Key] = node.Value;   // a rank of zero is the absence of one
            if (owned.Count > 0) data[kv.Key] = owned;
        }
        bag.Set("Ranks", data);
    }

    public void Load(SaveBag bag)
    {
        _ranks.Clear();

        foreach (var kv in bag.Get("Ranks", new Dictionary<string, Dictionary<string, int>>()))
            if (!string.IsNullOrEmpty(kv.Key) && kv.Value != null)
                _ranks[kv.Key] = new Dictionary<string, int>(kv.Value);

        if (_ranks.Count > 0) return;

        // A save written before ranks existed: every node in it was bought once, which is rank 1. Read only
        // when the new key found nothing, so it cannot fight a save that has both — and it is what stops a
        // player who already spent thirty levels finding the tree empty.
        foreach (var kv in bag.Get("Bought", new Dictionary<string, List<string>>()))
        {
            if (string.IsNullOrEmpty(kv.Key) || kv.Value == null) continue;

            var byNode = new Dictionary<string, int>();
            foreach (var id in kv.Value)
                if (!string.IsNullOrEmpty(id)) byNode[id] = 1;
            if (byNode.Count > 0) _ranks[kv.Key] = byNode;
        }
    }
}
