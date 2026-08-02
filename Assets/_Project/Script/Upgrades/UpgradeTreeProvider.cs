using VContainer;

// Trees come out of the ConfigRegistry by the character's id, exactly the way MCConfigProvider fetches that
// character's stats. ConfigRegistry keys by exact type before id, so both live under "MC 1" without meeting.
public class UpgradeTreeProvider : IGetUpgradeTree
{
    readonly ConfigRegistry _configs;

    [Inject]
    public UpgradeTreeProvider(ConfigRegistry configs) => _configs = configs;

    public UpgradeTreeConfig Get(string characterId)
        => string.IsNullOrEmpty(characterId) ? null : _configs.Get<UpgradeTreeConfig>(characterId);
}
