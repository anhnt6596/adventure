using UnityEngine;
using Lean.Pool;

// A ranged blow: on the hit frame it spits a slow homing soul-fire that seeks the nearest hostile in range,
// drifts over, and burns on contact. Range and the hit live here (the flame carries the owner's damage + team);
// the flight lives in SoulFire.
//
// An AttackAbility like every other blow, so it takes a slot the same way: Attack for a creature whose whole
// attack is this, None for a step of a combo or a weapon its AI throws.
public class SoulFireAttack : AttackAbility
{
    [SerializeField] float range = 6f;                  // how far the flame will hunt for a target
    [SerializeField] float speed = 6f;                  // flight speed — the shot's, not the caster's
    [SerializeField] float knockback = 3f;              // shove on the target when the flame lands; 0 = none
    [SerializeField] Transform muzzle;                  // the mouth; empty = this object
    [SerializeField] SoulFire flamePrefab;              // the soul-fire visual + homing (assign the fx)

    Vector3 Muzzle => muzzle != null ? muzzle.position : transform.position;

    int Team => Owner != null ? Owner.Team : Teams.Universal;   // ownerless -> belongs to no side, so it hits everything

    protected override void Start()
    {
        base.Start();

        if (flamePrefab == null)
            Debug.LogError($"[{nameof(SoulFireAttack)}] no {nameof(SoulFire)} prefab assigned — nothing to spit.", this);
    }

    protected override void Land()
    {
        if (flamePrefab == null) return;

        float damage = Owner != null ? Owner.AttackPower : 0f;
        Vector3 dir = Owner != null ? Owner.FacingDir : transform.forward;   // the shot flies (and starts seeking) this way
        var flame = LeanPool.Spawn(flamePrefab, Muzzle, Quaternion.identity);
        flame.Launch(new Shot(dir, Team, damage, speed, range, knockback, Owner));
    }
}
