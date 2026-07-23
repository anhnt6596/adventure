// Wall over ConfigRegistry: the one narrow way to fetch a main-character config by id. PlayerSystem depends on
// this, never on the whole registry, so no "grab any config by id" service-locator surface leaks into it.
public interface IGetMCConfig
{
    MainCharStatsConfig Get(string id);
}
