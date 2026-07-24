using Cysharp.Threading.Tasks;
using UnityEngine;
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

    public async UniTask WarpAsync(string mapId, int gateIndex)
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
            PlaceAtGate(_current, gateIndex);
            return;
        }

        // Cross-map: block input for the swap (released when this method returns).
        using var _ = _gate.Block(InputKind.All, "map-change");

        // TODO transition FX in

        // Keep the old map on screen while the new one loads + builds, then swap in one frame — no
        // blank gap between "old destroyed" and "new shown".
        var old = _current;

        var req = Resources.LoadAsync<GameObject>($"Maps/{mapId}");
        await req;

        if (req.asset is not GameObject prefab)
        {
            Debug.LogError($"[MapService] no map prefab at Resources/Maps/{mapId} (kept the current map).");
            return;
        }

        // Instantiate + inject the whole hierarchy, so Portals get IMapService and zones join the field.
        _current = _container.Instantiate(prefab);
        CurrentMapId = mapId;

        WireMapToScene(_current);
        PlaceAtGate(_current, gateIndex);

        // Only now remove the old map — same synchronous frame the new one is ready, so it's never blank.
        if (old != null)
        {
            old.SetActive(false);   // unregister its collision bodies before destroy
            Object.Destroy(old);
        }

        // Freeing the old map's assets is deferred (small 2D maps + cut transition). If memory grows:
        //   await Resources.UnloadUnusedAssets();   // full sweep — hide it behind the transition FX

        // TODO transition FX out
    }

    // Point the collision world at the loaded map's terrain. The map's obstacle bodies register themselves
    // (CollisionBody.OnEnable), and SetTerrain re-applies the new pass mask to them — so no per-body wiring.
    void WireMapToScene(GameObject map)
    {
        var terrain = map.GetComponentInChildren<TerrainGrid>(true);
        if (terrain != null) CollisionSystem.Instance?.SetTerrain(terrain);
        else Debug.LogWarning($"[MapService] map '{CurrentMapId}' has no TerrainGrid — tile collision disabled.", map);
    }

    void PlaceAtGate(GameObject mapInstance, int gateIndex)
    {
        var map = mapInstance.GetComponent<Map>();
        if (map == null)
        {
            Debug.LogError($"[MapService] map '{CurrentMapId}' has no Map component on its root prefab.", mapInstance);
            return;
        }

        var gate = map.GetGate(gateIndex);
        var player = _player.Current;
        if (gate != null && player != null)
        {
            player.transform.SetPositionAndRotation(gate.SpawnPosition, gate.SpawnRotation);
            _camera?.SnapToTarget();   // cut the camera to the new spot instead of sliding across
        }
    }
}
