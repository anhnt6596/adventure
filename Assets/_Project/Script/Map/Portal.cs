using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;
using VContainer;

// A departure zone: step in, and you're sent to a spawn point. Leave targetMapId empty to warp within the
// current map (no reload); set it to send the player to another map.
//
// Travel only. A Portal moves the player around ONE continuous world; a Gate opens a run, which is a whole
// lifetime with its own clock and its own wallet. Neither is a special case of the other.
public class Portal : InteractZone
{
    [Tooltip("Where to send the player, by map id (Resources/Maps).\n\n" +
             "LEAVE IT EMPTY to warp WITHIN this map: no reload, no input block, and nothing the player has " +
             "already done to the map is lost. Same thing happens if you type the current map's own id.\n\n" +
             "Fill it in and the map is swapped instead.")]
    [SerializeField] string targetMapId;

    [Tooltip("Which SpawnPoint the player lands on, as an index into the target Map's Spawn Points array.\n\n" +
             "The point must BE in that array — drop one in the scene and it is still invisible to this until " +
             "the Map component collects it (right-click Map > Collect Spawn Points From Children).\n\n" +
             "Do not land the player inside any Portal's zone, this one included: they would be sent straight " +
             "on again, and two portals facing each other bounce the player forever.")]
    [FormerlySerializedAs("targetGateIndex")]
    [SerializeField] int targetSpawnIndex;

    IMapService _maps;

    [Inject]
    public void ConstructPortal(IMapService maps) => _maps = maps;

    public override void OnActorEnter(MCController actor)
    {
        _maps.WarpAsync(targetMapId, targetSpawnIndex).Forget();
    }
}
