using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public class MapService : IMapService
{
    readonly IObjectResolver _container;
    readonly IInputGate _gate;
    readonly Character _player;

    GameObject _current;
    public string CurrentMapId { get; private set; } = "";

    [Inject]
    public MapService(IObjectResolver container, IInputGate gate, Character player)
    {
        _container = container;
        _gate = gate;
        _player = player;
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

        if (_current != null)
        {
            Object.Destroy(_current);
            _current = null;
        }

        var req = Resources.LoadAsync<GameObject>($"Maps/{mapId}");
        await req;

        if (req.asset is not GameObject prefab)
        {
            Debug.LogError($"[MapService] no map prefab at Resources/Maps/{mapId}");
            return;
        }

        // Instantiate + inject the whole hierarchy, so Portals get IMapService and zones join the field.
        _current = _container.Instantiate(prefab);
        CurrentMapId = mapId;

        PlaceAtGate(_current, gateIndex);

        // Freeing the old map's assets is deferred (small 2D maps + cut transition). If memory grows:
        //   await Resources.UnloadUnusedAssets();   // full sweep — hide it behind the transition FX

        // TODO transition FX out
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
        if (gate != null && _player != null)
            _player.transform.SetPositionAndRotation(gate.SpawnPosition, gate.SpawnRotation);
    }
}
