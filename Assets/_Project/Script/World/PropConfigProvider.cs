using VContainer;

// The only class that touches ConfigRegistry for prop configs. PropConfig is keyed by id in the registry like
// EnemyConfig — one config path for every hittable.
public class PropConfigProvider : IGetPropConfig
{
    readonly ConfigRegistry _configs;

    [Inject]
    public PropConfigProvider(ConfigRegistry configs) => _configs = configs;

    public IDamageableConfig Get(string id) => _configs.Get<PropConfig>(id);
}
