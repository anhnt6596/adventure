// What a picker exposes so a pickup's payload can deliver into the right store. The picker provides the
// stores; the payload picks which one it needs, so the picker stays generic.
public interface IPickupReceiver
{
    Inventory Inventory { get; }
    // Wallet Wallet { get; }   // added when currency arrives
}
