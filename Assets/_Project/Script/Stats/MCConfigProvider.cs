using System.Collections.Generic;
using System.Linq;
using VContainer;

// The only class that touches ConfigRegistry for MC configs. Change the body to move WHERE the config comes
// from (registry by id today; a save slot or server later) without touching PlayerSystem.
public class MCConfigProvider : IGetMCConfig
{
    readonly ConfigRegistry _configs;
    IReadOnlyList<string> _ids;

    [Inject]
    public MCConfigProvider(ConfigRegistry configs) => _configs = configs;

    public MainCharStatsConfig Get(string id) => _configs.Get<MainCharStatsConfig>(id);

    // Cached: the registry is baked at edit time, so the set can't change while playing.
    public IReadOnlyList<string> Ids => _ids ??= _configs.All<MainCharStatsConfig>().Select(c => c.Id).ToList();
}
