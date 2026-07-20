using System;
using UnityEngine;

// Bridge from a world pickup to the HUD's fly-to-UI, without injecting into pooled pickup objects.
// The HUD subscribes while shown; a pickup just fires Request with what it granted. When no handler
// is listening (menu, no HUD) pickups fall back to their world fly visual.
public static class PickupFly
{
    // worldHeight = the pickup's on-ground size, so the flying icon can start matching the scene.
    public static event Action<Vector3, ResourceDef, int, float> Requested;

    public static bool HasHandler => Requested != null;

    public static void Request(Vector3 worldPos, ResourceDef def, int amount, float worldHeight)
    {
        if (def != null && amount > 0) Requested?.Invoke(worldPos, def, amount, worldHeight);
    }
}
