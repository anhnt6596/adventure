using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Core.Save;

// The one owner of the player character: spawns it from an id, holds the live reference, and persists WHICH
// character is current. It spawns into a CHILD SCOPE that carries the id's stats + inventory config, so every
// injected component on the prefab (Character, Picker, attacks) receives them through its normal [Inject] —
// no per-component wiring here.
public class PlayerSystem : IPlayer, IStartable, ISavable
{
    const string DefaultId = "MC 1";

    readonly IGetMCConfig _mcConfig;
    readonly PrefabRegistry _prefabs;
    readonly IObjectResolver _container;
    readonly SaveService _save;

    string _currentId = DefaultId;
    IScopedObjectResolver _scope;   // per-spawn scope holding this body's stats/config; disposed on the next spawn

    public Character Current { get; private set; }
    public bool Exists => Current != null;
    public Vector3 Position => Current != null ? Current.transform.position : Vector3.zero;
    public event Action<Character> Spawned;

    public string SaveKey => "player";

    [Inject]
    public PlayerSystem(IGetMCConfig mcConfig, PrefabRegistry prefabs, IObjectResolver container, SaveService save)
    {
        _mcConfig = mcConfig;
        _prefabs = prefabs;
        _container = container;
        _save = save;
        _save.Register(this);   // runs Load() now → _currentId from save (DefaultId on a fresh game)
    }

    public void Start() => Spawn(_currentId);

    // Swap the player to another character id and persist the choice.
    public void SwitchTo(string id)
    {
        _currentId = id;
        Spawn(id);
        _save.Save(SaveKey);
    }

    void Spawn(string id)
    {
        var ident = _prefabs.Get(id);
        var cfg = _mcConfig.Get(id);
        if (ident == null || cfg == null)
        {
            Debug.LogError($"[{nameof(PlayerSystem)}] can't spawn '{id}' — {(ident == null ? "no prefab" : "no config")} in registry.");
            return;
        }

        // tear down the previous body + its scope (respawn / switch)
        if (Current != null) UnityEngine.Object.Destroy(Current.gameObject);
        _scope?.Dispose();

        var stats = new MainCharStats(cfg);
        _scope = _container.CreateScope(b =>
        {
            b.RegisterInstance<ICharacterStats>(stats);
            b.RegisterInstance<IInventoryConfig>(cfg);
        });

        // NOTE: _scope.Instantiate() would re-route injection through the parent LifetimeScope (which lacks
        // ICharacterStats) — instantiate plainly, then inject with THIS scope so the per-spawn stats bind.
        var go = UnityEngine.Object.Instantiate(ident.gameObject);
        _scope.InjectGameObject(go);
        Current = go.GetComponent<Character>();
        Spawned?.Invoke(Current);
    }

    public void Save(SaveBag bag) => bag.Set("current", _currentId);
    public void Load(SaveBag bag) => _currentId = bag.Get("current", DefaultId);
}
