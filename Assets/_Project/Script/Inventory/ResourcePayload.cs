using UnityEngine;

// A pickup that grants a countable resource into the receiver's Inventory, which is uncapped - so this
// always delivers in full and never leaves a remainder. `_remaining` is still reset on every spawn: the
// serialized `amount` is only the default, and a pooled piece must not come back carrying a stale count.
[DisallowMultipleComponent]
public class ResourcePayload : MonoBehaviour, IPickupPayload
{
    [SerializeField] ResourceDef resource;
    [SerializeField, Min(1)] int amount = 1;   // default stack size

    int _remaining;

    void OnEnable() => _remaining = amount;    // fresh count on (re)spawn - pool-safe

    public int Amount => _remaining;
    public void SetAmount(int value) => _remaining = Mathf.Max(0, value);

    public bool CanDeliver(IPickupReceiver receiver) => resource != null;

    // Stores the lot and reports itself consumed. The shape (take what fits, report leftovers) is kept
    // because a capped store still uses it - a supply bag can refuse, and a home chest may later.
    public bool Deliver(IPickupReceiver receiver)
    {
        if (resource == null) return true;
        int taken = receiver.Inventory.Add(resource, _remaining);
        _remaining -= taken;
        if (taken > 0)
        {
            var sr = GetComponentInChildren<SpriteRenderer>();
            Vector3 spawnPos = sr != null ? sr.bounds.center : transform.position;   // the art's center, not the ground pivot
            float worldSize = sr != null ? Mathf.Max(sr.bounds.size.x, sr.bounds.size.y) : 1f;
            PickupFly.RequestResource(spawnPos, resource, taken, worldSize);   // fly an icon to the HUD
        }
        return _remaining <= 0;
    }
}
