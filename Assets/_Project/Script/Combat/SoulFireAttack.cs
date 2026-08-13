using UnityEngine;
using Lean.Pool;

// A ranged attack: spits a slow homing soul-fire that seeks the nearest hostile in range, drifts over, and
// burns on contact. Range + the hit live here (the flame carries the owner's damage + team); the AI/player
// only triggers it via the owner's Attack(). One attack = one class, like ShapeAttack; the flight lives in
// SoulFire.
public class SoulFireAttack : MonoBehaviour
{
    [SerializeField] float range = 6f;                  // how far the flame will hunt for a target
    [SerializeField] float speed = 6f;                  // flight speed — the shot's, not the caster's
    [SerializeField] float knockback = 3f;              // shove on the target when the flame lands; 0 = none
    [SerializeField] UnitAnimator animatorSource;  // drag the one on the art child; fires Hit at the spit frame

    [Tooltip("Which animation this spits on. Its hit frame is the moment the flame leaves the mouth.")]
    [SerializeField] AnimAction anim = AnimAction.Attack;
    [SerializeField] Transform muzzle;                  // the mouth; empty = this object
    [SerializeField] SoulFire flamePrefab;              // the soul-fire visual + homing (assign the fx)

    Vector3 Muzzle => muzzle != null ? muzzle.position : transform.position;

    DynamicUnit _owner;                                 // the flame hunts targets not on this unit's team, deals its AttackPower
    int Team => _owner != null ? _owner.Team : Teams.Universal;   // ownerless -> belongs to no side, so it hits everything

    void Awake() => _owner = GetComponentInParent<DynamicUnit>();

    void OnEnable()  { if (animatorSource != null) animatorSource.Hit += Spit; }
    void OnDisable() { if (animatorSource != null) animatorSource.Hit -= Spit; }

    void Start()
    {
        if (animatorSource == null)
            Debug.LogError($"[{nameof(SoulFireAttack)}] no CharacterAnimator — the flame never spits. Assign it (on the art child).", this);
        if (flamePrefab == null)
            Debug.LogError($"[{nameof(SoulFireAttack)}] no SoulFire prefab assigned — nothing to spit.", this);
    }

    // Fires when the animation reaches its spit frame (via UnitAnimator.Hit). This one's own clip only —
    // anything else the unit can play that has a hit frame is not a spit.
    void Spit(AnimAction action)
    {
        if (action != anim || flamePrefab == null) return;

        float damage = _owner != null ? _owner.AttackPower : 0f;
        Vector3 dir = _owner != null ? _owner.FacingDir : transform.forward;   // the shot flies (and starts seeking) this way
        var flame = LeanPool.Spawn(flamePrefab, Muzzle, Quaternion.identity);
        flame.Launch(new Shot(dir, Team, damage, speed, range, knockback, _owner));
    }
}
