using System;
using System.Collections.Generic;
using Core.Save;
using VContainer;

// Which upgrade nodes each character has bought, and the arithmetic of paying for them.
//
// THE BOUGHT SET IS THE ONLY STATE, and everything else is derived from it: what is unlocked comes from the
// set plus the tree's `requires`, and how many points are left comes from the set plus the character's level.
// Nothing here stores "unlocked" or "points remaining" — a stored copy would be a second answer, free to
// disagree with the first the moment a price or a link is edited in the inspector. Same rule PayGateSystem
// keeps by storing deposits and never a paid flag.
//
// POINTS ARE LEVELS, AND A NODE IS A POINT. A character has as many points as levels, every node costs one,
// so "spent" is a count and "left" is level minus that count. Nothing about the price is authored or saved,
// and RESET is only "forget the set" — there is no pool to hand anything back to, which is why a full respec
// is three lines rather than a refund ledger.
public class UpgradeSystem : ISavable
{
    readonly Dictionary<string, HashSet<string>> _bought = new Dictionary<string, HashSet<string>>();
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

    public bool IsBought(string characterId, UpgradeNode node)
    {
        if (node == null) return false;
        return !string.IsNullOrEmpty(characterId)
               && _bought.TryGetValue(characterId, out var set)
               && set.Contains(node.id);
    }

    // ANY one of the nodes leading in opens this one, not all of them. Letting two branches converge is only
    // worth doing if arriving by either is enough — under AND the second route buys the player nothing except
    // a longer wait, and the shape stops meaning anything.
    public bool IsUnlocked(string characterId, UpgradeTreeConfig tree, UpgradeNode node)
    {
        if (tree == null || node == null) return false;

        // No requirements = it hangs off the implicit centre, so it is open from the first level.
        if (node.requires == null || node.requires.Length == 0) return true;

        foreach (var requiredId in node.requires)
        {
            var required = tree.Find(requiredId);
            if (required != null && IsBought(characterId, required)) return true;
        }
        return false;
    }

    // One node, one point — so what has been spent IS how many nodes are bought. Counted off the tree rather
    // than off the saved set, so a node deleted from the config stops charging for itself.
    public int Spent(string characterId, UpgradeTreeConfig tree)
    {
        if (tree?.nodes == null) return 0;

        int spent = 0;
        foreach (var node in tree.nodes)
            if (node != null && IsBought(characterId, node)) spent++;
        return spent;
    }

    public int Available(string characterId, UpgradeTreeConfig tree)
        => _levels.Level(characterId) - Spent(characterId, tree);

    public bool CanBuy(string characterId, UpgradeTreeConfig tree, UpgradeNode node)
        => node != null
           && !IsBought(characterId, node)
           && IsUnlocked(characterId, tree, node)
           && Available(characterId, tree) >= 1;

    public bool Buy(string characterId, UpgradeTreeConfig tree, UpgradeNode node)
    {
        if (string.IsNullOrEmpty(characterId) || !CanBuy(characterId, tree, node)) return false;

        if (!_bought.TryGetValue(characterId, out var set))
            _bought[characterId] = set = new HashSet<string>();

        set.Add(node.id);
        _save.Save(SaveKey);
        Changed?.Invoke();

        // TODO: apply the node's effect. Nothing to apply yet — a node carries no effect until stats grow a
        // way of being modified from outside (Docs/TODO.md). When it lands, the place it takes hold is
        // PlayerSystem.Spawn, where MainCharStats is built: stats are rebuilt per spawn, so upgrades are
        // applied there and NEVER have to be un-applied — including on Reset below.
        return true;
    }

    // Full respec. Points are levels, so nothing has to be given back: dropping the set is what makes them
    // spendable again. The character keeps its level, and therefore its total.
    public void Reset(string characterId)
    {
        if (string.IsNullOrEmpty(characterId)) return;
        if (!_bought.TryGetValue(characterId, out var set) || set.Count == 0) return;

        set.Clear();
        _save.Save(SaveKey);
        Changed?.Invoke();
    }

    public void Save(SaveBag bag)
    {
        var data = new Dictionary<string, List<string>>();
        foreach (var kv in _bought)
            if (kv.Value.Count > 0) data[kv.Key] = new List<string>(kv.Value);
        bag.Set("Bought", data);
    }

    public void Load(SaveBag bag)
    {
        _bought.Clear();
        foreach (var kv in bag.Get("Bought", new Dictionary<string, List<string>>()))
            if (!string.IsNullOrEmpty(kv.Key) && kv.Value != null)
                _bought[kv.Key] = new HashSet<string>(kv.Value);
    }
}
