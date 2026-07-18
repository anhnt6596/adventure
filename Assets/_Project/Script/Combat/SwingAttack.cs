using System.Collections.Generic;
using UnityEngine;
using VContainer;

// MC1's attack: after the sword swing, everything in a circle around the character takes damage.
// The circle is centred on the character (so wood will later fly outward, away from this point).
// One attack = one class — a different weapon's attack is its own component, not a config of this one.
public class SwingAttack : MonoBehaviour
{
    [SerializeField] float radius = 1.5f;               // reach — a property of this weapon, not a character stat
    [SerializeField] int team = 1;                      // player's normal attack; team 0 would be a self-hurting skill (bomb)
    [SerializeField] CharacterAnimator animatorSource;  // drag the one on the art child; fires Hit at the connect frame

    CombatWorld _combat;
    ICharacterStats _stats;
    readonly List<IDamageable> _hits = new List<IDamageable>();

    [Inject]
    public void Construct(CombatWorld combat, ICharacterStats stats)
    {
        _combat = combat;
        _stats = stats;
    }

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
        if (_combat == null)
            Debug.LogError($"[{nameof(SwingAttack)}] CombatWorld not injected — add this GameObject to GameScope's Auto Inject Game Objects.", this);
        if (animatorSource == null)
            Debug.LogError($"[{nameof(SwingAttack)}] no CharacterAnimator found — the swing will never land. Assign it (it's on the art child).", this);
        if (_stats == null)
            Debug.LogError($"[{nameof(SwingAttack)}] ICharacterStats not injected — damage will be 0.", this);
    }

    // Fires when the animation reaches its hit frame (via CharacterAnimator.Hit).
    void OnSwingHit()
    {
        if (_combat == null) return;

        _combat.Rebuild();
        _combat.Overlap(transform.position, radius, team, _hits);

        float damage = _stats != null ? _stats.AttackPower.Value : 0f;
        for (int i = 0; i < _hits.Count; i++)
            _hits[i].TakeDamage(damage, this);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.3f, 0.6f);
        const int seg = 28;
        Vector3 c = transform.position;
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
