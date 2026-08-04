using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Core.Save;

// The one owner of the player character: spawns it from an id, holds the live reference, and persists WHICH
// character is current. It spawns into a CHILD SCOPE that carries the id's stats + inventory config, so every
// injected component on the prefab (MC, Picker, attacks) receives them through its normal [Inject] —
// no per-component wiring here.
public class PlayerSystem : IPlayer, IStartable, ISavable
{
    const string DefaultId = "MC 1";

    readonly IGetMCConfig _mcConfig;
    readonly PrefabRegistry _prefabs;
    readonly IObjectResolver _container;
    readonly SaveService _save;
    readonly IGetUpgradeTree _trees;
    readonly UpgradeSystem _upgrades;

    string _currentId = DefaultId;
    IScopedObjectResolver _scope;   // per-spawn scope holding this body's stats/config; disposed on the next spawn
    MainCharStats _stats;           // the live set, kept so a node bought mid-game can land on it now

    public MCController Current { get; private set; }
    public bool Exists => Current != null;
    public Vector3 Position => Current != null ? Current.transform.position : Vector3.zero;
    public event Action<MCController> Spawned;

    public string SaveKey => "player";

    [Inject]
    public PlayerSystem(IGetMCConfig mcConfig, PrefabRegistry prefabs, IObjectResolver container, SaveService save,
                        IGetUpgradeTree trees, UpgradeSystem upgrades)
    {
        _mcConfig = mcConfig;
        _prefabs = prefabs;
        _container = container;
        _save = save;
        _trees = trees;
        _upgrades = upgrades;
        _save.Register(this);   // runs Load() now → _currentId from save (DefaultId on a fresh game)

        // Buying a node has to show up NOW — the popup is open, nothing respawns, and an upgrade you cannot
        // feel until you next die is a bug nobody reports for a month.
        _upgrades.Changed += ApplyUpgrades;
    }

    public void Start()
    {
        if (Spawn(_currentId)) return;
        if (_currentId == DefaultId) return;   // the fallback itself failed — nothing left to try

        // A saved id that can no longer spawn (config renamed, prefab removed) would otherwise leave the game
        // with no player at all, permanently. Fall back so the next save heals the choice.
        Debug.LogWarning($"[{nameof(PlayerSystem)}] saved character '{_currentId}' can't spawn — falling back to '{DefaultId}'.");
        _currentId = DefaultId;
        Spawn(_currentId);
    }

    // Swap the player to another character id, and only then record the choice: a failed switch must not
    // leave a dead id behind. SaveService saves everything on quit, so a dirty _currentId would be persisted
    // even without the explicit Save here — and would spawn nothing on the next run.
    public void SwitchTo(string id)
    {
        if (!Spawn(id)) return;
        _currentId = id;
        _save.Save(SaveKey);
    }

    // True only once a live MC is Current. Everything is checked BEFORE the old body comes down, so a
    // bad id leaves the current player standing instead of destroying it and finding out afterwards.
    bool Spawn(string id)
    {
        var ident = _prefabs.Get(id);
        if (ident == null)
        {
            Debug.LogError($"[{nameof(PlayerSystem)}] can't spawn '{id}' — no prefab with that id in the registry.");
            return false;
        }

        // The registry holds every Identifiable prefab, not just characters — a prop or enemy id must not
        // get this far, or we'd tear down the player for a body that can never be one.
        if (ident.GetComponent<MCController>() == null)
        {
            Debug.LogError($"[{nameof(PlayerSystem)}] can't spawn '{id}' — its prefab has no {nameof(MCController)}.");
            return false;
        }

        var cfg = _mcConfig.Get(id);
        if (cfg == null)
        {
            Debug.LogError($"[{nameof(PlayerSystem)}] can't spawn '{id}' — no config with that id in the registry.");
            return false;
        }

        // A swap takes over where the old body stood, so switching character doesn't teleport the player.
        // Read the pose before destroying it; with no body yet (first spawn) fall back to the prefab's own
        // transform — identical to a plain Instantiate — and let MapService move it to the map's gate.
        bool hasBody = Current != null;
        var position = hasBody ? Current.transform.position : ident.transform.position;
        var rotation = hasBody ? Current.transform.rotation : ident.transform.rotation;

        // tear down the previous body + its scope (respawn / switch)
        if (hasBody) UnityEngine.Object.Destroy(Current.gameObject);
        _scope?.Dispose();

        _stats = new MainCharStats(cfg);
        _scope = _container.CreateScope(b =>
        {
            // Both faces of ONE registration: everything that only READS a stat asks for ICharacterStats and
            // cannot modify one; the few components that genuinely move a stat (Hunger pausing the drain)
            // ask for MainCharStats, and asking for it is the declaration.
            //
            // As().AsSelf() rather than two RegisterInstance calls — RegisterInstance also registers the
            // concrete type, so registering it twice puts MainCharStats in the registry twice and the scope
            // refuses to build. Same shape GameScope already uses for PlayerSystem itself.
            b.RegisterInstance(_stats).As<ICharacterStats>().AsSelf();
            b.RegisterInstance<IHungerConfig>(cfg);
            b.RegisterInstance<IDamageableConfig>(cfg);   // MC's HP — MCController exposes it to its Damageable
        });

        // Before the body is built, so Damageable's Start reads a maximum that already includes them.
        ApplyUpgrades();

        // NOTE: _scope.Instantiate() would re-route injection through the parent LifetimeScope (which lacks
        // ICharacterStats) — instantiate plainly, then inject with THIS scope so the per-spawn stats bind.
        var go = UnityEngine.Object.Instantiate(ident.gameObject, position, rotation);
        _scope.InjectGameObject(go);
        Current = go.GetComponent<MCController>();   // guaranteed by the prefab check above
        Spawned?.Invoke(Current);
        return true;
    }

    // Everything the tree adds is tagged with this one object, so re-applying is "take all of mine off, put
    // all of mine back" — one code path that serves buying, resetting and respawning alike. Three separate
    // paths would be three chances for them to disagree about what is currently on the character.
    static readonly object UpgradeSource = new object();

    void ApplyUpgrades()
    {
        if (_stats == null) return;

        _stats.RemoveBySource(UpgradeSource);

        var tree = _trees?.Get(_currentId);
        if (tree?.nodes == null) return;

        var context = new UpgradeContext(_stats, UpgradeSource);
        foreach (var node in tree.nodes)
            if (node?.effect != null && _upgrades.IsBought(_currentId, node))
                node.effect.Apply(context);
    }

    public void Save(SaveBag bag) => bag.Set("current", _currentId);
    public void Load(SaveBag bag) => _currentId = bag.Get("current", DefaultId);
}
