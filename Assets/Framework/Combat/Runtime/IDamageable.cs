using UnityEngine;

public interface IDamageable
{
    Vector3 Position { get; }

    // How easy it is to hit, which is a feel knob - deliberately not the collision radius, which is
    // how much room the body takes up.
    float HitRadius { get; }

    bool IsAlive { get; }
    int Team { get; }

    void TakeDamage(float amount, object source);

    // A hit can shove the target. `shove` is direction × DISTANCE — its length is how far a body of mass 1
    // would be pushed, in world units. The receiver scales it down by its own mass, so a heavy thing moves
    // proportionally less and an immovable one not at all. No length → no shove.
    void ApplyKnockback(Vector3 shove);
}
