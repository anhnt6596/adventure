using UnityEngine;

// A swing that throws something forward as it lands — the sword-wind of a finisher, a shockwave off a hammer.
// It is ONE blow and one component: the sweep and the wave leave on the same hit frame because they are the
// same swing, and a combo runs exactly one of these per press.
//
// A SUBCLASS OF ShapeAttack, not two components bolted together, for the same reason BackstepSkill is a DashSkill
// that also throws: what would be shared is nearly all of it — the shape, the reach, the knockback, the arming,
// the clip — and the difference is that something also leaves the blade. Two components would need to agree
// about which clip they land on, with nothing to make them.
//
// WHAT FLIES IS A PREFAB. A wind blade, a shockwave and a spirit slash differ in what is drawn and in nothing
// else, so there is no class per wave — see ProjectileSkill, which settles the same question the same way.
public class WaveAttack : ShapeAttack
{
    [Header("The wave")]
    [Tooltip("What flies out. Any Projectile: this hands it a Shot and never learns what it is.")]
    [SerializeField] Projectile wave;

    [Tooltip("Where it leaves from — the blade, the hand. Empty = this object.")]
    [SerializeField] Transform muzzle;

    [Tooltip("World units per second.")]
    [SerializeField, Min(0f)] float speed = 10f;

    [Tooltip("How long it stays out, in seconds. How FAR it reaches falls out of this and the speed, so " +
             "anything that makes it faster also sends it further.")]
    [SerializeField, Min(0f)] float life = 0.7f;

    [Tooltip("Shove dealt by the wave. Separate from the sweep's own knockback: what the blade shoves and " +
             "what the wind shoves are two different blows landing on two different things.")]
    [SerializeField, Min(0f)] float waveKnockback = 0f;

    Vector3 Muzzle => muzzle != null ? muzzle.position : transform.position;

    // Overridden rather than declared afresh: two private Start methods in one hierarchy and Unity calls only
    // the lower one, quietly dropping the base's check that this blow has an animator at all.
    protected override void Start()
    {
        base.Start();

        if (wave == null)
            Debug.LogError($"[{nameof(WaveAttack)}] no {nameof(Projectile)} assigned — it sweeps and nothing " +
                           "leaves the blade.", this);
    }

    // The sweep first, then the wave. Both on this one frame, off the same swing.
    protected override void Land()
    {
        base.Land();

        if (wave == null || Owner == null) return;

        // TIME BECOMES DISTANCE HERE, at the one place that knows both numbers: the wave is handed a length of
        // ground and counts it down as it goes, which no slow frame can lengthen, while the swing still authors
        // the flight as the duration it reads as.
        var shot = new Shot(Owner.FacingDir, Owner.Team, Owner.AttackPower, speed, life * speed,
                            waveKnockback, Owner);
        Projectile.Fan(wave, Muzzle, shot, 1, 0f);
    }

    // Where the wave starts, drawn along the way it will go — the reach is a thing you judge against the map.
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.9f, 1f, 0.9f);
        Gizmos.DrawLine(Muzzle, Muzzle + Facing * (speed * life));
    }
}
