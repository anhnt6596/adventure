using System.Diagnostics;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;
using VContainer;
using VContainer.Unity;

public class MapService : IMapService
{
    readonly IObjectResolver _container;
    readonly IInputGate _gate;
    readonly IPlayer _player;
    readonly CameraRig _camera;

    GameObject _current;
    public string CurrentMapId { get; private set; } = "";

    [Inject]
    public MapService(IObjectResolver container, IInputGate gate, IPlayer player, CameraRig camera)
    {
        _container = container;
        _gate = gate;
        _player = player;
        _camera = camera;
    }

    public async UniTask WarpAsync(string mapId, int spawnIndex, IObjectResolver into = null)
    {
        bool sameMap = string.IsNullOrEmpty(mapId) || mapId == CurrentMapId;

        // In-map warp: no reload, no input block, map state (trees, etc.) untouched.
        if (sameMap)
        {
            if (_current == null)
            {
                Debug.LogError("[MapService] in-map warp requested but no map is loaded.");
                return;
            }
            PlaceAtSpawnPoint(_current, spawnIndex);
            return;
        }

        // Cross-map: block input for the swap (released when this method returns).
        using var _ = _gate.Block(InputKind.All, "map-change");

        // TODO transition FX in

        // Keep the old map on screen while the new one loads + builds, then swap in one frame — no
        // blank gap between "old destroyed" and "new shown".
        var old = _current;

        // Phase timings: a map swap is a visible stall, so each step reports its own cost rather than
        // one total that says "slow" without saying where.
        var watch = Stopwatch.StartNew();

        var req = Resources.LoadAsync<GameObject>($"Maps/{mapId}");
        await req;
        long loadMs = watch.ElapsedMilliseconds;

        if (req.asset is not GameObject prefab)
        {
            Debug.LogError($"[MapService] no map prefab at Resources/Maps/{mapId} (kept the current map).");
            return;
        }

        // Instantiate + inject the whole hierarchy, so Portals get IMapService and zones join the field.
        //
        // Instantiate plainly and inject afterwards, rather than resolver.Instantiate: on a CHILD scope the
        // latter routes injection through the parent and the child's own registrations are never seen. Same
        // trap PlayerSystem.Spawn documents for the per-character scope.
        _current = Object.Instantiate(prefab);
        (into ?? _container).InjectGameObject(_current);
        CurrentMapId = mapId;
        long instantiateMs = watch.ElapsedMilliseconds - loadMs;

        WireMapToScene(_current);
        PlaceAtSpawnPoint(_current, spawnIndex);
        long wireMs = watch.ElapsedMilliseconds - loadMs - instantiateMs;

        // Only now remove the old map — same synchronous frame the new one is ready, so it's never blank.
        if (old != null)
        {
            old.SetActive(false);   // unregister its collision bodies before destroy
            Object.Destroy(old);
        }
        long destroyMs = watch.ElapsedMilliseconds - loadMs - instantiateMs - wireMs;

        Debug.Log($"[MapService] '{mapId}' loaded in {watch.ElapsedMilliseconds}ms " +
                  $"(Resources.LoadAsync {loadMs}ms, Instantiate+inject {instantiateMs}ms, " +
                  $"wire+place {wireMs}ms, destroy old {destroyMs}ms).");

        // Freeing the old map's assets is deferred (small 2D maps + cut transition). If memory grows:
        //   await Resources.UnloadUnusedAssets();   // full sweep — hide it behind the transition FX

        // TODO transition FX out
    }

    // Point the scene's world systems at the loaded map's terrain. The map's obstacle bodies register themselves
    // (CollisionBody.OnEnable), and SetTerrain re-applies the new pass mask to them — so no per-body wiring.
    // The border fog needs the same grid for a different reason: its darkness is anchored to the map's edge.
    void WireMapToScene(GameObject map)
    {
        // The map's own light palette, or null for "look like the world does". Written on every swap, so a
        // map without one cannot inherit the last map's sky.
        var descriptor = map.GetComponent<Map>();
        DayNightLighting.MapPalette = descriptor != null ? descriptor.Lighting : null;

        var terrain = map.GetComponentInChildren<TerrainGrid>(true);
        MapBorderFog.Terrain = terrain;
        if (terrain != null) CollisionSystem.Instance?.SetTerrain(terrain);
        else Debug.LogWarning($"[MapService] map '{CurrentMapId}' has no TerrainGrid — tile collision disabled.", map);
    }

    void PlaceAtSpawnPoint(GameObject mapInstance, int spawnIndex)
    {
        var map = mapInstance.GetComponent<Map>();
        if (map == null)
        {
            Debug.LogError($"[MapService] map '{CurrentMapId}' has no Map component on its root prefab.", mapInstance);
            return;
        }

        var spawn = map.GetSpawnPoint(spawnIndex);
        var player = _player.Current;
        if (spawn != null && player != null)
        {
            player.transform.SetPositionAndRotation(spawn.SpawnPosition, spawn.SpawnRotation);
            _camera?.SnapToTarget();   // cut the camera to the new spot instead of sliding across
        }
    }
}
