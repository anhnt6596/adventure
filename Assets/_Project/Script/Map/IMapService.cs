using Cysharp.Threading.Tasks;
using VContainer;

// Swaps the live map prefab inside GameScene. Maps are referenced by id (Resources/Maps/{id}),
// never by direct prefab reference, so only the current map is in memory.
public interface IMapService
{
    string CurrentMapId { get; }

    // Put the player at spawn point `spawnIndex` of map `mapId`.
    // - mapId empty or equal to the current map -> in-map warp: just reposition, no reload.
    // - otherwise -> swap the map (input blocked for the swap).
    //
    // `into` is the scope the new map is injected through; null means the game scope, which is what an
    // overworld map wants. An arena is passed the run's scope instead, so the things authored into it —
    // build spots, resource nodes — resolve the run's services and die with the run. The map is geometry
    // either way; the scope is what decides which world it is part of.
    UniTask WarpAsync(string mapId, int spawnIndex, IObjectResolver into = null);
}
