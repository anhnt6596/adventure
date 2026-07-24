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

    // A hit can shove the target. `impulse` is the attack's force (direction × strength); the receiver
    // scales it by its own mass, so an immovable thing just absorbs it. No force → no shove.
    void ApplyKnockback(Vector3 impulse);
}
