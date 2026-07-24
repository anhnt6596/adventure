using System.Collections.Generic;
using UnityEngine;

// A melee-style attack: after the swing, everything in a circle around the owner takes damage. The circle is
// centred on the owner (so wood flies outward from here). Range + the hit both live here; the AI (or player
// input) only pulls the trigger via the owner's Attack(). One attack = one class.
public class SwingAttack : MonoBehaviour
{
    [SerializeField] float radius = 1.5f;               // reach — a property of this weapon, not a character stat
    [SerializeField] float knockback = 5f;              // shove dealt outward from the swing centre; 0 = none
    [SerializeField] UnitAnimator animatorSource;  // drag the one on the art child; fires Hit at the connect frame
    [SerializeField] Transform origin;                  // centre of the swing; empty = this object

    Vector3 Origin => origin != null ? origin.position : transform.position;

    DynamicUnit _owner;                                 // fights for whoever owns it — its Team, its AttackPower
    int Team => _owner != null ? _owner.Team : 0;
    readonly List<IDamageable> _hits = new List<IDamageable>();

    void Awake() => _owner = GetComponentInParent<DynamicUnit>();

    void OnEnable()
    {
        if (animatorSource != null) animatorSource.Hit += OnSwingHit;
    }

    void OnDisable()
    {
        if (animatorSource != null) animatorSource.Hit -= OnSwingHit;
    }

    void Start()
    {
        if (animatorSource == null)
            Debug.LogError($"[{nameof(SwingAttack)}] no CharacterAnimator found — the swing will never land. Assign it (it's on the art child).", this);
    }

    // Fires when the animation reaches its hit frame (via UnitAnimator.Hit).
    void OnSwingHit()
    {
        CombatWorld.Instance.Rebuild();
        CombatWorld.Instance.Overlap(Origin, radius, Team, _hits);

        float damage = _owner != null ? _owner.AttackPower : 0f;
        for (int i = 0; i < _hits.Count; i++)
        {
            var hit = _hits[i];
            hit.TakeDamage(damage, this);
            if (knockback > 0f)
            {
                Vector3 push = hit.Position - Origin;   // outward from the swing centre
                push.y = 0f;
                if (push.sqrMagnitude > 1e-6f) hit.ApplyKnockback(push.normalized * knockback);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.3f, 0.6f);
        const int seg = 28;
        Vector3 c = Origin;
        Vector3 prev = c + new Vector3(radius, 0, 0);
        for (int i = 1; i <= seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg;
            Vector3 next = c + new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}
