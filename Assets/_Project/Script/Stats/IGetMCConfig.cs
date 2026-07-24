using System.Collections.Generic;

// Wall over ConfigRegistry: the one narrow way to reach main-character configs. PlayerSystem depends on this,
// never on the whole registry, so no "grab any config by id" service-locator surface leaks into it.
public interface IGetMCConfig
{
    MainCharStatsConfig Get(string id);

    // Every main-char kind, for anything that offers a choice — the cheat panel today, character select later.
    IReadOnlyList<string> Ids { get; }
}
