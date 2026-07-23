using VContainer;

// The only class that touches ConfigRegistry for MC configs. Change the body to move WHERE the config comes
// from (registry by id today; a save slot or server later) without touching PlayerSystem.
public class MCConfigProvider : IGetMCConfig
{
    readonly ConfigRegistry _configs;

    [Inject]
    public MCConfigProvider(ConfigRegistry configs) => _configs = configs;

    public MainCharStatsConfig Get(string id) => _configs.Get<MainCharStatsConfig>(id);
}
