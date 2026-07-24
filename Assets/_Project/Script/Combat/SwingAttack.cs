using System.Collections.Generic;
using UnityEngine;
using VContainer;

// MC1's attack: after the sword swing, everything in a circle around the character takes damage.
// The circle is centred on the character (so wood will later fly outward, away from this point).
// One attack = one class — a different weapon's attack is its own component, not a config of this one.
public class SwingAttack : MonoBehaviour
{
    [SerializeField] float radius = 1.5f;               // reach — a property of this weapon, not a character stat
    [SerializeField] float knockback = 5f;              // shove dealt outward from the swing centre; 0 = none
    [SerializeField] UnitAnimator animatorSource;  // drag the one on the art child; fires Hit at the connect frame
    [SerializeField] Transform origin;                  // centre of the swing; empty = this object

    Vector3 Origin => origin != null ? origin.position : transform.position;

    ICharacterStats _stats;
    UnitController _owner;                               // the swing fights for whoever owns it (MC 1 / enemy 2)
    int Team => _owner != null ? _owner.Team : 0;
    readonly List<IDamageable> _hits = new List<IDamageable>();

    [Inject]
    public void Construct(ICharacterStats stats) => _stats = stats;

    void Awake() => _owner = GetComponentInParent<UnitController>();

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
        if (_stats == null)
            Debug.LogError($"[{nameof(SwingAttack)}] ICharacterStats not injected — damage will be 0.", this);
    }

    // Fires when the animation reaches its hit frame (via CharacterAnimator.Hit).
    void OnSwingHit()
    {
        CombatWorld.Instance.Rebuild();
        CombatWorld.Instance.Overlap(Origin, radius, Team, _hits);

        float damage = _stats != null ? _stats.AttackPower.Value : 0f;
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
