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

    public async UniTask ChangeMapAsync(string mapId, int gateIndex)
    {
        // Block input for the whole swap (released when this method returns).
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

        var map = _current.GetComponent<Map>();
        if (map == null)
        {
            Debug.LogError($"[MapService] map '{mapId}' has no Map component on its root prefab.", _current);
            return;
        }

        var gate = map.GetGate(gateIndex);
        if (gate != null && _player != null)
            _player.transform.SetPositionAndRotation(gate.SpawnPosition, gate.SpawnRotation);

        // Freeing the old map's assets is deferred (small 2D maps + cut transition). If memory grows:
        //   await Resources.UnloadUnusedAssets();   // full sweep — hide it behind the transition FX

        // TODO transition FX out
    }
}
