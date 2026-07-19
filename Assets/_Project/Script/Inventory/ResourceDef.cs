using UnityEngine;

// A countable resource (wood, stone, ...). A Config, so it has an Id and lives in the ConfigRegistry: the
// inventory keys on the ResourceDef at runtime and persists by Id (Load resolves it back via the registry).
// The display name is view — derived from the Id later, not stored here.
[CreateAssetMenu(menuName = "Inventory/Resource")]
public class ResourceDef : Config
{
}
