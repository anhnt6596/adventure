# Config System

ScriptableObject config management: opt-in auto-registry by inheritance, type-keyed O(1) lookup, editor bake tool. Factories inject the registry and resolve configs by id — no god object, entities receive their resolved config.

## Pieces

### `Config` (base)
Abstract SO. Inherit to opt a config type into the auto-registry.

```csharp
public abstract class Config : ScriptableObject
{
    [SerializeField] string id;
    public string Id => string.IsNullOrEmpty(id) ? name : id;   // empty id => asset name
}
```

### `ConfigRegistry`
One asset holding every collected `Config`. Lookup keyed by concrete type + id.

- `T Get<T>(string id)` — O(1), returns null if missing.
- `IEnumerable<T> All<T>()` — enumerate a type (matches subclasses via `is T`).
- Lazy `Build()` fills `Dictionary<Type, Dictionary<string, Config>>` on first access.
- `SetAll(List<Config>)` — editor-only, filled by the bake tool.

### `ConfigRegistryBuilder` (editor)
`Tools > Config > Rebuild Registry` — `AssetDatabase.FindAssets("t:Config")` gathers all `Config`-derived assets and bakes them into the single `ConfigRegistry` asset (`t:` matches derived types too).

## Workflow

1. Define a config type — inherit `Config`:
   ```csharp
   [CreateAssetMenu(menuName = "Config/Enemy")]
   public class EnemyConfig : Config { public int hp; public float damage; }
   ```
2. Create assets: `Create > Config > Enemy`. Leave `Id` empty to use the asset name, or set it (e.g. `goblin`).
3. Create one `ConfigRegistry` asset: `Create > Config > Registry`.
4. Bake: `Tools > Config > Rebuild Registry`.
5. Register in DI (App / scope):
   ```csharp
   builder.RegisterInstance(_configRegistry);
   ```
6. Factory injects + resolves:
   ```csharp
   class EnemyFactory
   {
       [Inject] ConfigRegistry _configs;
       public Enemy Spawn(string id)
       {
           var cfg = _configs.Get<EnemyConfig>(id);
           ...
       }
   }
   ```

## Rules & caveats

- **Singular configs stay plain `ScriptableObject`** (e.g. `MainCharStatsConfig`) — do NOT inherit `Config`, don't bake them; drag them directly where needed.
- **Keys are concrete types.** `Get<EnemyConfig>` won't find a `BossConfig : EnemyConfig` (it's keyed under `BossConfig`). Configs are meant to be flat; if you need base-type lookup, use `All<EnemyConfig>()` (uses `is T`).
- **Rebuild after adding/renaming configs** (same discipline as the UI registry). If this gets annoying, add an `AssetPostprocessor` to auto-rebuild on config import/save.
- Editor-only code (`ConfigRegistryBuilder`, `SetAll`) is guarded by `#if UNITY_EDITOR` so it strips from builds.

## Why not a god `GameConfig`

Only factories/spawners touch the registry; they inject it and hand each entity its own resolved config. The registry is a type-keyed dictionary, not a per-type property bag — adding a config type needs zero registry changes.
