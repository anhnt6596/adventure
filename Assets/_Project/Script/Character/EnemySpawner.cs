using UnityEngine;
using VContainer;
using VContainer.Unity;

// Turns an id into a live, configured enemy: prefab by id (PrefabRegistry) + config by id (ConfigRegistry),
// instantiate, inject its scene deps (IPlayer for tactics, ...), then bind the config. The one place enemies
// are made — a spawn zone calls this. Factories are the sanctioned registry consumers (see CONFIG.md), so
// touching the registries here is fine — the surface doesn't leak into the enemies themselves.
public class EnemySpawner
{
    readonly PrefabRegistry _prefabs;
    readonly ConfigRegistry _configs;
    readonly IObjectResolver _container;

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
        _container.InjectGameObject(go);          // IPlayer for tactics, etc. (CombatWorld is a static singleton)
        var enemy = go.GetComponent<EnemyController>();
        enemy.Configure(cfg);
        return enemy;
    }
}
