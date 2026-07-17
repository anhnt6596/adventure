using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

// A departure zone: step in, and you're sent to another map's gate. Lives inside a map prefab.
public class Portal : InteractZone
{
    [SerializeField] string targetMapId;
    [SerializeField] int targetGateIndex;

    IMapService _maps;
    bool _travelling;

    [Inject]
    public void ConstructPortal(IMapService maps) => _maps = maps;

    public override void OnActorEnter(Character actor)
    {
        if (_travelling) return;                 // don't re-fire while the swap is in flight
        if (string.IsNullOrEmpty(targetMapId))
        {
            Debug.LogError($"[Portal] '{name}' has no targetMapId.", this);
            return;
        }
        _travelling = true;
        _maps.ChangeMapAsync(targetMapId, targetGateIndex).Forget();
    }
}
