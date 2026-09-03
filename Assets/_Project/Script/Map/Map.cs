using UnityEngine;
using UnityEngine.Serialization;

// Sits on the root of a map prefab: what the scene needs to know about the map it just loaded. Spawn points,
// so an arriving MapService can put the player down at the right one, and the palette the place is lit by.
public class Map : MonoBehaviour
{
    [FormerlySerializedAs("gates")]
    [SerializeField] SpawnPoint[] spawnPoints;

    [Tooltip("How this place is lit across a day. Leave empty to use the scene's default (GameScope) — set " +
             "it only when this map wants its own look: a cave that never gets bright, an arena with a " +
             "longer night.")]
    [SerializeField] DayLightConfig lighting;

    // Null is a real answer, not a missing reference: most maps look like the world does, and the scene's
    // default is what that means. MapService hands this to DayNightLighting on load.
    public DayLightConfig Lighting => lighting;

    public int SpawnPointCount => spawnPoints != null ? spawnPoints.Length : 0;

    public SpawnPoint GetSpawnPoint(int index)
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError($"[Map] '{name}' has no spawn points.", this);
            return null;
        }
        if (index < 0 || index >= spawnPoints.Length)
        {
            Debug.LogError($"[Map] '{name}' spawn index {index} out of range (0..{spawnPoints.Length - 1}); using 0.", this);
            index = 0;
        }
        return spawnPoints[index];
    }

    [ContextMenu("Collect Spawn Points From Children")]
    void CollectSpawnPoints() => spawnPoints = GetComponentsInChildren<SpawnPoint>(true);
}
