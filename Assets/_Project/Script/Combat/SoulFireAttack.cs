using UnityEngine;
using VContainer;
using Lean.Pool;

// The mage MC's normal attack: spits a slow homing soul-fire from the mouth. The flame seeks the nearest
// hostile in range, drifts over, and burns on contact - re-targeting if its mark dies first, fizzling if
// nothing is in range. One attack = one class, like SwingAttack; the flight itself lives in SoulFire.
public class SoulFireAttack : MonoBehaviour
{
    [SerializeField] float range = 6f;                  // how far the flame will hunt for a target
    [SerializeField] float speed = 4f;                  // drift speed
    [SerializeField] float knockback = 3f;              // shove on the target when the flame lands; 0 = none
    [SerializeField] UnitAnimator animatorSource;  // drag the one on the art child; fires Hit at the spit frame
    [SerializeField] Transform muzzle;                  // the mouth; empty = this object
    [SerializeField] SoulFire flamePrefab;              // the soul-fire visual + homing (assign the fx)

    Vector3 Muzzle => muzzle != null ? muzzle.position : transform.position;

    ICharacterStats _stats;
    UnitController _owner;                               // the flame hunts targets not on this unit's team
    int Team => _owner != null ? _owner.Team : 0;

    [Inject]
    public void Construct(ICharacterStats stats) => _stats = stats;

    void Awake() => _owner = GetComponentInParent<UnitController>();

    void OnEnable()  { if (animatorSource != null) animatorSource.Hit += Spit; }
    void OnDisable() { if (animatorSource != null) animatorSource.Hit -= Spit; }

    void Start()
    {
        if (animatorSource == null)
            Debug.LogError($"[{nameof(SoulFireAttack)}] no CharacterAnimator — the flame never spits. Assign it (on the art child).", this);
        if (flamePrefab == null)
            Debug.LogError($"[{nameof(SoulFireAttack)}] no SoulFire prefab assigned — nothing to spit.", this);
    }

    // Fires when the animation reaches its spit frame (via CharacterAnimator.Hit). The flame does its own
    // targeting through the CombatWorld hash, so an attack with nothing in range just fizzles immediately.
    void Spit()
    {
        if (flamePrefab == null) return;

        float damage = _stats != null ? _stats.AttackPower.Value : 0f;
        var flame = LeanPool.Spawn(flamePrefab, Muzzle, Quaternion.identity);
        flame.Launch(transform, range, Team, damage, speed, knockback);
    }
}
