using System.Collections.Generic;
using Core.Save;
using UnityEngine;
using VContainer;
using VContainer.Unity;

// Who pays experience, and to whom. CharacterLevels owns the curve and the banking; this owns the RULES —
// what an act is worth, and whether it has already been paid for.
//
// TWO KINDS OF AWARD, and the split is the whole design:
//
//     Award      — repeatable, to the character that did it. Killing something is this.
//     AwardOnce  — a FIRST, paid once for the whole save: a map entered, a kind killed, a gate opened.
//
// Firsts are what the pillar asks for. Level is upgrade points is power, and Docs/DESIGN.md says the day a
// safe map out-farms the frontier the drop table is broken — so the bulk of experience has to come from
// going somewhere new, which cannot be farmed by construction. The per-kill bounty stays small enough that
// standing in a cleared field is a poor living.
//
// A FIRST IS BANKED INTO A WORLD TOTAL rather than handed to whoever happened to be out. Levels are per
// character (see CharacterLevels), so if firsts belonged to a character too, unlocking a second one would
// leave it at level 1 with two ways up: walk every map again — the backtracking DESIGN.md refuses — or never
// catch up. Instead each character remembers how much of the world's discovery it has taken, and is topped up
// to the total when it is played. A character you have never touched arrives knowing what the world knows;
// what it does NOT get is the kill experience earned by the body you actually played, so the one you play
// stays a little ahead. That is the difference worth having.
//
// THE TOTAL IS STORED, NOT RECOMPUTED FROM THE KEYS. Recomputing would mean looking every first back up in
// the configs, so the day a map's discovery value is retuned every existing save silently re-levels. Same
// reason CharacterLevels stores progress-into-level rather than a running total: a retune must change what
// the NEXT thing is worth and nothing else.
//
// Keys are namespaced by what kind of first they are — "kind:mewfrog", and later "map:Map_2",
// "gate:bridge_1". Nothing parses them; the prefix is there so a key collision between two different kinds
// of thing cannot happen by accident.
public class ExperienceSystem : IStartable, ISavable
{
    readonly HashSet<string> _firsts = new HashSet<string>();       // acts already paid for, world-wide
    readonly Dictionary<string, int> _absorbed = new Dictionary<string, int>();   // character -> discovery taken
    int _discovery;                                                  // world total banked by firsts

    readonly SaveService _save;
    readonly CharacterLevels _levels;
    readonly IPlayer _player;

    public string SaveKey => "experience";

    [Inject]
    public ExperienceSystem(SaveService save, CharacterLevels levels, IPlayer player)
    {
        _save = save;
        _levels = levels;
        _player = player;
        _save.Register(this);   // loads _firsts / _discovery / _absorbed

        _player.Spawned += OnSpawned;
    }

    // A body may already be standing by the time this starts — entry points are started in registration
    // order, and a missed catch-up would leave a character short until its next spawn.
    public void Start()
    {
        if (_player.Exists) CatchUp(_player.Current.Id);
    }

    void OnSpawned(MCController mc) => CatchUp(mc != null ? mc.Id : null);

    // The character being played, by the id its prefab carries — the same id CharacterLevels banks under and
    // PlayerSystem spawns from.
    string CurrentId => _player.Current != null ? _player.Current.Id : null;

    // Repeatable, and it goes to whoever is out there. Doing nothing when nobody is spawned is right rather
    // than defensive: an award with no character to receive it has no meaning to save for later.
    public void Award(int amount) => _levels.AddExp(CurrentId, amount);

    // True the first time only. The act is recorded even when it is worth nothing, because "has this been
    // done" is the question the set answers — a value of zero is a tuning choice, not a reason to ask again.
    public bool AwardOnce(string key, int amount)
    {
        if (string.IsNullOrEmpty(key) || !_firsts.Add(key)) return false;

        if (amount > 0) _discovery += amount;
        Absorb(CurrentId);      // the character out there feels it now, not on its next spawn
        _save.Save(SaveKey);    // and the first is recorded even if nobody was there to take it
        return true;
    }

    // Top a character up to everything the world has discovered. Idempotent: a character already level with
    // the total is owed nothing, which is what makes it safe to call on every spawn.
    public void CatchUp(string characterId)
    {
        if (Absorb(characterId)) _save.Save(SaveKey);
    }

    bool Absorb(string characterId)
    {
        if (string.IsNullOrEmpty(characterId)) return false;

        int taken = _absorbed.TryGetValue(characterId, out int n) ? n : 0;
        int owed = _discovery - taken;
        if (owed <= 0) return false;

        _absorbed[characterId] = _discovery;
        _levels.AddExp(characterId, owed);
        return true;
    }

    // Flat collections, like CharacterLevels and PayGateSystem: the save round-trips through Newtonsoft, and
    // string->int is the shape already proven there.
    public void Save(SaveBag bag)
    {
        bag.Set("Firsts", new List<string>(_firsts));
        bag.Set("Discovery", _discovery);
        bag.Set("Absorbed", new Dictionary<string, int>(_absorbed));
    }

    public void Load(SaveBag bag)
    {
        _firsts.Clear();
        _absorbed.Clear();

        foreach (var key in bag.Get("Firsts", new List<string>()))
            if (!string.IsNullOrEmpty(key)) _firsts.Add(key);

        _discovery = Mathf.Max(0, bag.Get("Discovery", 0));

        foreach (var kv in bag.Get("Absorbed", new Dictionary<string, int>()))
            if (!string.IsNullOrEmpty(kv.Key)) _absorbed[kv.Key] = Mathf.Max(0, kv.Value);
    }
}
