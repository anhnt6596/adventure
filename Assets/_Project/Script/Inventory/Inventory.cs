using System.Collections.Generic;
using UnityEngine;

// A capped count store keyed by ResourceDef at runtime. Owned by an InventorySystem (which names it by id
// and persists it) — so it is plain data, NOT ISavable itself. Capacity comes from an IInventoryConfig,
// so the same store serves the main char, a home storage, an NPC, ...
public class Inventory
{
    readonly Dictionary<ResourceDef, int> _counts = new Dictionary<ResourceDef, int>();
    readonly IInventoryConfig _config;
    int _total;

    public string Id { get; }
    public event System.Action Changed;

    public Inventory(string id, IInventoryConfig config)
    {
        Id = id;
        _config = config;
    }

    public int Capacity => _config.Capacity;
    public int Total => _total;
    public int SpaceLeft => Mathf.Max(0, Capacity - _total);
    public IReadOnlyDictionary<ResourceDef, int> Counts => _counts;

    public int Get(ResourceDef def) => def != null && _counts.TryGetValue(def, out var n) ? n : 0;

    // Total cap for now — a full backpack has no room for any resource. Per-type stacks/slots come later.
    public int SpaceFor(ResourceDef def) => SpaceLeft;

    // Adds up to what fits; returns the amount actually stored (0 if full).
    public int Add(ResourceDef def, int amount)
    {
        if (def == null || amount <= 0) return 0;

        int added = Mathf.Min(amount, SpaceLeft);
        if (added <= 0) return 0;

        _counts[def] = Get(def) + added;
        _total += added;
        Changed?.Invoke();
        return added;
    }

    // Removes up to `amount` of a resource; returns how many were actually removed (0 if none held).
    public int Remove(ResourceDef def, int amount)
    {
        if (def == null || amount <= 0) return 0;

        int removed = Mathf.Min(amount, Get(def));
        if (removed <= 0) return 0;

        int left = Get(def) - removed;
        if (left > 0) _counts[def] = left;
        else _counts.Remove(def);
        _total -= removed;
        Changed?.Invoke();
        return removed;
    }

    // Restore saved counts as-is (no capacity clamp — the saved state was already valid).
    public void Restore(Dictionary<ResourceDef, int> counts)
    {
        _counts.Clear();
        _total = 0;
        foreach (var kv in counts) { _counts[kv.Key] = kv.Value; _total += kv.Value; }
        Changed?.Invoke();
    }
}
