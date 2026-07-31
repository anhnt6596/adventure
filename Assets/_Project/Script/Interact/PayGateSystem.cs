using System.Collections.Generic;
using VContainer;
using Core.Save;

// What has been fed into each pay gate so far: gate id -> resource id -> count. Saved under one key.
//
// DEPOSITS ARE THE ONLY STATE. There is no "paid" flag anywhere, because a gate being paid for is just its
// deposits meeting its price — and a stored flag would be a second answer to that, free to disagree with the
// first the moment somebody edits a cost in the inspector.
//
// KEYED BY AN AUTHORED ID, not by position or by name. A gate is authored INTO a map, so the existing
// "player-placed things are player data, keyed by (map, position)" rule does not fit it: position keying
// breaks the day you nudge the gate a unit, name keying breaks on a rename, and index keying breaks the
// moment a second gate is inserted before it. All three break silently, months later, against save files
// that already exist. An id somebody typed survives all of it.
//
// Progress is permanent, and so is the finished gate: Docs/DESIGN.md's ratchet says ground you have cleared
// stays cleared, so a bridge you had to buy twice would be exactly the backtracking the pillar refuses.
public class PayGateSystem : ISavable
{
    readonly Dictionary<string, Dictionary<string, int>> _deposits = new Dictionary<string, Dictionary<string, int>>();
    readonly SaveService _save;

    public string SaveKey => "paygates";

    [Inject]
    public PayGateSystem(SaveService save)
    {
        _save = save;
        _save.Register(this);   // loads _deposits
    }

    public int Deposited(string gateId, string resourceId)
    {
        if (string.IsNullOrEmpty(gateId) || string.IsNullOrEmpty(resourceId)) return 0;
        return _deposits.TryGetValue(gateId, out var byRes) && byRes.TryGetValue(resourceId, out int n) ? n : 0;
    }

    public void Deposit(string gateId, string resourceId, int amount)
    {
        if (string.IsNullOrEmpty(gateId) || string.IsNullOrEmpty(resourceId) || amount <= 0) return;

        if (!_deposits.TryGetValue(gateId, out var byRes))
            _deposits[gateId] = byRes = new Dictionary<string, int>();

        byRes[resourceId] = (byRes.TryGetValue(resourceId, out int n) ? n : 0) + amount;
        _save.Save(SaveKey);
    }

    public void Save(SaveBag bag) => bag.Set("Deposits", _deposits);

    public void Load(SaveBag bag)
    {
        _deposits.Clear();
        foreach (var kv in bag.Get("Deposits", new Dictionary<string, Dictionary<string, int>>()))
            if (!string.IsNullOrEmpty(kv.Key) && kv.Value != null) _deposits[kv.Key] = kv.Value;
    }
}
