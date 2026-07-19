// Whatever provides an inventory's capacity — a character config, a home-storage config, an NPC config.
// Reused across anything that owns a backpack/store, so an Inventory depends on this, not on full stats.
public interface IInventoryConfig
{
    int Capacity { get; }
}
