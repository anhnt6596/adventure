using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

// A departure zone: step in, and you're sent to a gate. Leave targetMapId empty to warp within the
// current map (no reload); set it to send the player to another map.
public class Portal : InteractZone
{
    [SerializeField] string targetMapId;      // empty = warp inside this map
    [SerializeField] int targetGateIndex;

    IMapService _maps;

    [Inject]
    public void ConstructPortal(IMapService maps) => _maps = maps;

    public override void OnActorEnter(Character actor)
    {
        _maps.WarpAsync(targetMapId, targetGateIndex).Forget();
    }
}
