using Cysharp.Threading.Tasks;

// Swaps the live map prefab inside GameScene. Maps are referenced by id (Resources/Maps/{id}),
// never by direct prefab reference, so only the current map is in memory.
public interface IMapService
{
    string CurrentMapId { get; }

    // Load map `mapId`, place the player at its gate `gateIndex`. Input is blocked for the swap.
    UniTask ChangeMapAsync(string mapId, int gateIndex);
}
