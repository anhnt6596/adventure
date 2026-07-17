using Cysharp.Threading.Tasks;

// Swaps the live map prefab inside GameScene. Maps are referenced by id (Resources/Maps/{id}),
// never by direct prefab reference, so only the current map is in memory.
public interface IMapService
{
    string CurrentMapId { get; }

    // Put the player at gate `gateIndex` of map `mapId`.
    // - mapId empty or equal to the current map -> in-map warp: just reposition, no reload.
    // - otherwise -> swap the map (input blocked for the swap).
    UniTask WarpAsync(string mapId, int gateIndex);
}
