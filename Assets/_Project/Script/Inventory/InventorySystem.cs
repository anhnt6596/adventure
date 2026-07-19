using System.Collections.Generic;
using VContainer;
using Core.Save;

// Owns every Inventory, keyed by id, and persists them all under ONE save file — so a second inventory
// (NPC, home storage) is just another id, no save-key collision. Resources are stored by ResourceDef.Id
// and resolved back to defs via ConfigRegistry on load.
public class InventorySystem : ISavable
{
    readonly Dictionary<string, Inventory> _inventories = new Dictionary<string, Inventory>();
    readonly Dictionary<string, Dictionary<string, int>> _saved = new Dictionary<string, Dictionary<string, int>>();
    readonly SaveService _save;
    readonly ConfigRegistry _configs;

    public string SaveKey => "inventories";

    [Inject]
    public InventorySystem(SaveService save, ConfigRegistry configs)
    {
        _save = save;
        _configs = configs;
        _save.Register(this);   // loads _saved
    }

    // The store for `id`, created on first ask with its capacity config and any saved counts applied.
    public Inventory GetOrCreate(string id, IInventoryConfig config)
    {
        if (_inventories.TryGetValue(id, out var inv)) return inv;

        inv = new Inventory(id, config);
        if (_saved.TryGetValue(id, out var byResId))
            inv.Restore(Resolve(byResId));           // apply saved counts (fires Changed, but no sub yet)
        inv.Changed += () => _save.Save(SaveKey);    // subscribe AFTER restore so it doesn't self-save
        _inventories[id] = inv;
        return inv;
    }

    Dictionary<ResourceDef, int> Resolve(Dictionary<string, int> byResId)
    {
        var result = new Dictionary<ResourceDef, int>();
        foreach (var kv in byResId)
        {
            var def = _configs.Get<ResourceDef>(kv.Key);
            if (def != null) result[def] = kv.Value;   // unknown id → dropped
        }
        return result;
    }

    public void Save(SaveBag bag)
    {
        var data = new Dictionary<string, Dictionary<string, int>>();
        foreach (var kv in _inventories)
        {
            var byResId = new Dictionary<string, int>();
            foreach (var c in kv.Value.Counts) byResId[c.Key.Id] = c.Value;
            data[kv.Key] = byResId;
        }
        bag.Set("Inventories", data);
    }

    public void Load(SaveBag bag)
    {
        _saved.Clear();
        foreach (var kv in bag.Get("Inventories", new Dictionary<string, Dictionary<string, int>>()))
            _saved[kv.Key] = kv.Value;

        foreach (var kv in _inventories)                // usually none exist yet at load time
            if (_saved.TryGetValue(kv.Key, out var byResId))
                kv.Value.Restore(Resolve(byResId));
    }
}
