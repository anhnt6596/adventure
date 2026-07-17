using UnityEngine;

// Sits on the root of a map prefab. Its only job here is to expose the gates (spawn points)
// by index, so an arriving MapService can place the player at the right one.
public class Map : MonoBehaviour
{
    [SerializeField] Gate[] gates;

    public int GateCount => gates != null ? gates.Length : 0;

    public Gate GetGate(int index)
    {
        if (gates == null || gates.Length == 0)
        {
            Debug.LogError($"[Map] '{name}' has no gates.", this);
            return null;
        }
        if (index < 0 || index >= gates.Length)
        {
            Debug.LogError($"[Map] '{name}' gate index {index} out of range (0..{gates.Length - 1}); using 0.", this);
            index = 0;
        }
        return gates[index];
    }

    [ContextMenu("Collect Gates From Children")]
    void CollectGates() => gates = GetComponentsInChildren<Gate>(true);
}
