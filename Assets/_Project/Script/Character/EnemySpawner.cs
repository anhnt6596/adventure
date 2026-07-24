using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;

// Turns an id into a live, configured enemy: prefab by id (PrefabRegistry) + config by id (ConfigRegistry),
// instantiate, then inject through a scope that carries the config — so EnemyController gets it via [Inject],
// the same way PlayerSystem's scope feeds the MC its stats. Nothing about the config sits on the prefab.
//
// MC uses one scope per spawn (there's a single player). Enemies are many, so a scope-per-instance would leak
// one per spawn; the config is shared per kind, so instead one scope is cached per kind and every enemy of it
// injects through that. Scopes are few (one per kind) and dispose with the game scope.
public class EnemySpawner
{
    readonly PrefabRegistry _prefabs;
    readonly ConfigRegistry _configs;
    readonly IObjectResolver _container;
    readonly Dictionary<EnemyConfig, IScopedObjectResolver> _scopes = new Dictionary<EnemyConfig, IScopedObjectResolver>();

    [Inject]
    public EnemySpawner(PrefabRegistry prefabs, ConfigRegistry configs, IObjectResolver container)
    {
        _prefabs = prefabs;
        _configs = configs;
        _container = container;
    }

    public EnemyController Spawn(string id, Vector3 position, Quaternion rotation)
    {
        var ident = _prefabs.Get(id);
        if (ident == null || ident.GetComponent<EnemyController>() == null)
        {
            Debug.LogError($"[{nameof(EnemySpawner)}] no enemy prefab with id '{id}' (needs an {nameof(EnemyController)} on its root).");
            return null;
        }

        var cfg = _configs.Get<EnemyConfig>(id);
        if (cfg == null)
        {
            Debug.LogError($"[{nameof(EnemySpawner)}] no {nameof(EnemyConfig)} with id '{id}' in the registry.");
            return null;
        }

        var go = Object.Instantiate(ident.gameObject, position, rotation);
        ScopeFor(cfg).InjectGameObject(go);   // EnemyController gets its EnemyConfig; tactics get IPlayer; ...

        // The config doubles as the enemy's IDamageableConfig; bind it now (before any Start) so the body has
        // its HP. A placed thing would drag a DamageableConfig instead — same interface, different source.
        go.GetComponentInChildren<Damageable>(true)?.Bind(cfg);

        return go.GetComponent<EnemyController>();
    }

    // The child scope for a kind, created on first spawn and reused. It registers the kind's config and
    // inherits the game scope (IPlayer, etc.), so injecting through it wires both.
    IScopedObjectResolver ScopeFor(EnemyConfig cfg)
    {
        if (!_scopes.TryGetValue(cfg, out var scope))
        {
            scope = _container.CreateScope(b => b.RegisterInstance(cfg));
            _scopes[cfg] = scope;
        }
        return scope;
    }
}
