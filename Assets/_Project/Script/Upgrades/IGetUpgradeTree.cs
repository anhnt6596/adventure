// The only class allowed to touch ConfigRegistry for upgrade trees — same wall, and for the same reason, as
// IGetMCConfig sits in front of the character configs. Change the implementation to move WHERE a tree comes
// from without anything that reads trees noticing.
public interface IGetUpgradeTree
{
    // Keyed by the character's id, because a tree IS a character's. Null when that character has no tree
    // authored yet, which is a normal state while trees are being built one at a time.
    UpgradeTreeConfig Get(string characterId);
}
