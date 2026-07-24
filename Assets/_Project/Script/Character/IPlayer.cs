using System;
using UnityEngine;

// Read-only handle to the player. Anything that needs "the player" (map warp, camera, UI, AI) depends on this
// and reads Current, so a respawn or character-switch is a single reference change inside PlayerSystem.
public interface IPlayer
{
    MCController Current { get; }        // null until PlayerSystem has spawned — watch Spawned
    bool Exists { get; }
    Vector3 Position { get; }
    event Action<MCController> Spawned;  // fires each time a body comes into being (spawn / respawn / switch);
                                      // always carries a live MC — a failed spawn fires nothing
}
