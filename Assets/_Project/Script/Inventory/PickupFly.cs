using System;
using UnityEngine;

// Where a flying pickup icon lands on the HUD. A pickup grants exactly one kind of thing, so it knows its
// own slot; the HUD looks the element up, so the pickup still knows nothing about the UI.
public enum PickupSlot
{
    Bag,       // resources — lands on the backpack, or on that resource's row while the list is open
    Stomach,   // food — eaten where it lies, so it lands on the hunger bar
}

// One flight. `def` and `amount` are the BAG's business: they pick which row the icon homes to and which
// count ticks up when it arrives. Food has neither — it is not a countable thing you hold, and the fullness
// it gave was already added before this fired.
public struct PickupFlyRequest
{
    public Vector3 worldPos;     // where the piece is
    public float worldHeight;    // its on-ground size, so the icon can start out matching the scene
    public Sprite icon;
    public PickupSlot slot;
    public ResourceDef def;
    public int amount;
}

// Bridge from a world pickup to the HUD's fly-to-UI, without injecting into pooled pickup objects.
// The HUD subscribes while shown; a pickup just fires Request with what it granted. When no handler
// is listening (menu, no HUD) pickups fall back to their world fly visual.
public static class PickupFly
{
    public static event Action<PickupFlyRequest> Requested;

    public static bool HasHandler => Requested != null;

    public static void Request(PickupFlyRequest req)
    {
        if (req.icon != null) Requested?.Invoke(req);
    }

    public static void RequestResource(Vector3 worldPos, ResourceDef def, int amount, float worldHeight)
    {
        if (def == null || amount <= 0) return;
        Request(new PickupFlyRequest
        {
            worldPos = worldPos,
            worldHeight = worldHeight,
            icon = def.icon,
            slot = PickupSlot.Bag,
            def = def,
            amount = amount,
        });
    }

    public static void RequestFood(Vector3 worldPos, Sprite icon, float worldHeight)
        => Request(new PickupFlyRequest
        {
            worldPos = worldPos,
            worldHeight = worldHeight,
            icon = icon,
            slot = PickupSlot.Stomach,
        });
}
