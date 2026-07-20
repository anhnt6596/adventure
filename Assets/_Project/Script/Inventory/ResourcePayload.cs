using UnityEngine;

// A pickup that grants a countable resource into the receiver's (capped) Inventory. The live count is
// `_remaining`, reset on every spawn - the serialized `amount` is only the default, so a pooled piece
// never comes back carrying the leftover that Deliver subtracted in its previous life.
[DisallowMultipleComponent]
public class ResourcePayload : MonoBehaviour, IPickupPayload
{
    [SerializeField] ResourceDef resource;
    [SerializeField, Min(1)] int amount = 1;   // default stack size

    int _remaining;

    void OnEnable() => _remaining = amount;    // fresh count on (re)spawn - pool-safe

    public int Amount => _remaining;
    public void SetAmount(int value) => _remaining = Mathf.Max(0, value);

    public bool CanDeliver(IPickupReceiver receiver)
        => resource != null && receiver.Inventory.SpaceFor(resource) > 0;

    // Stores what fits, shrinking the remaining count. Returns true when nothing is left; a false means
    // the backpack filled up and the remainder stays in this pickup on the ground.
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
            PickupFly.Request(spawnPos, resource, taken, worldSize);   // fly an icon to the HUD
        }
        return _remaining <= 0;
    }
}
