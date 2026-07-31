using System.Collections.Generic;
using UnityEngine;

// A count store keyed by ResourceDef at runtime. Owned by an InventorySystem (which names it by id and
// persists it) — so it is plain data, NOT ISavable itself. The same store serves the main char, a home
// storage, an NPC, ...
//
// IT HAS NO CAPACITY, and that is a design decision rather than a thing not built yet. The pillar is
// exploration, and the tie-breaker is "does this make exploring better". A cap on loot fails it twice:
// finding something becomes bad news ("what do I throw away"), and it becomes a SECOND reason to end a
// trip, competing with the one the game actually wants — supplies running low, which is what turns route
// knowledge into a resource. The only ceiling in the game is the stomach (Hunger), and a stomach raises no
// such question. See Docs/DESIGN.md.
//
// It is also what keeps the whole inventory UI out of the game: a cap is only worth a screen when the
// player has to CHOOSE what to drop. Nothing here ever refuses, so nothing ever has to be dropped.
public class Inventory
{
    readonly Dictionary<ResourceDef, int> _counts = new Dictionary<ResourceDef, int>();
    int _total;

    public string Id { get; }
    public event System.Action Changed;

    public Inventory(string id) => Id = id;

    public int Total => _total;
    public IReadOnlyDictionary<ResourceDef, int> Counts => _counts;

    public int Get(ResourceDef def) => def != null && _counts.TryGetValue(def, out var n) ? n : 0;

    // Always takes the lot. The return value is kept so callers read the same as they always did, and so a
    // future store that CAN refuse (a home chest with a size) does not change the shape of the call.
    public int Add(ResourceDef def, int amount)
    {
        if (def == null || amount <= 0) return 0;

        _counts[def] = Get(def) + amount;
        _total += amount;
        Changed?.Invoke();
        return amount;
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

    // Restore saved counts as-is.
    public void Restore(Dictionary<ResourceDef, int> counts)
    {
        _counts.Clear();
        _total = 0;
        foreach (var kv in counts) { _counts[kv.Key] = kv.Value; _total += kv.Value; }
        Changed?.Invoke();
    }
}
