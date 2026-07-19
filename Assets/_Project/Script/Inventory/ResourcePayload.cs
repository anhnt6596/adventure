using UnityEngine;

// A pickup that grants a countable resource into the receiver's (capped) Inventory. `amount` is settable
// so a stack dropped out of the inventory can carry its count (one piece, N resources).
[DisallowMultipleComponent]
public class ResourcePayload : MonoBehaviour, IPickupPayload
{
    [SerializeField] ResourceDef resource;
    [SerializeField, Min(1)] int amount = 1;

    public int Amount => amount;
    public void SetAmount(int value) => amount = Mathf.Max(0, value);

    public bool CanDeliver(IPickupReceiver receiver)
        => resource != null && receiver.Inventory.SpaceFor(resource) > 0;

    // Stores what fits, shrinking `amount` by the stored count. Returns true when nothing is left; a
    // false means the backpack filled up and the remainder stays in this pickup on the ground.
    public bool Deliver(IPickupReceiver receiver)
    {
        if (resource == null) return true;
        amount -= receiver.Inventory.Add(resource, amount);
        return amount <= 0;
    }
}
