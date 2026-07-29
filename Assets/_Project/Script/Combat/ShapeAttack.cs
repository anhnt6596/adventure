using System.Collections.Generic;
using UnityEngine;

// An instant area attack: at the animation's hit frame, everything inside a shape takes damage. The shape is
// either a CIRCLE (a swing that lands all around the owner) or a RECT (a thrust or cleave that reaches down a
// lane in front of it) — same trigger, same damage, same knockback either way, so a new weapon is a field
// change rather than a new class. Reach and shape belong to THIS weapon, not to a character stat; the AI or
// player input only pulls the trigger via the owner's Attack().
//
// The rect is oriented by the owner's FacingDir, NOT by any transform: a billboard unit never rotates its
// transform, it only tracks a facing.
public class ShapeAttack : MonoBehaviour
{
    public enum Shape { Circle, Rect }

    [SerializeField] Shape shape = Shape.Circle;
    [SerializeField] float radius = 1.5f;                     // Circle: reach in every direction
    [SerializeField] Vector2 size = new Vector2(1.5f, 3f);    // Rect: x = width ACROSS the facing, y = length ALONG it
    [SerializeField] float forwardOffset = 0f;                // slide the shape this far along the facing; 0 = centred on the owner
    [SerializeField] float knockback = 5f;                    // shove dealt outward from the owner; 0 = none
    [SerializeField] UnitAnimator animatorSource;             // drag the one on the art child; fires Hit at the connect frame

    DynamicUnit _owner;                                       // fights for whoever owns it — its Team, its AttackPower
    int Team => _owner != null ? _owner.Team : Teams.Universal;   // ownerless -> belongs to no side, so it hits everything
    readonly List<IDamageable> _hits = new List<IDamageable>();

    // Anchored on THIS object's position — put the component wherever the attack should come from. There is
    // deliberately no anchor-Transform field: a transform offset is world-axis, and a billboard unit never
    // rotates its transform, so any non-zero one would pin the hitbox to a compass direction instead of to the
    // unit's facing. Facing-relative placement is what forwardOffset is for.
    // The fallback is +X (east), not +Z (north): it is only ever reached in edit mode, where Awake hasn't run
    // and there is no owner to have a facing. The 2D art is authored facing RIGHT, so drawing the box to the
    // right is the one orientation you can judge straight against the sprite sitting in front of you. Pointing
    // it north means doing the rotation in your head every time you tune forwardOffset.
    Vector3 Facing => _owner != null ? _owner.FacingDir : Vector3.right;
    Vector3 Centre => transform.position + Facing * forwardOffset;

    void Awake() => _owner = GetComponentInParent<DynamicUnit>();

    void OnEnable()
    {
        if (animatorSource != null) animatorSource.Hit += OnHit;
    }

    void OnDisable()
    {
        if (animatorSource != null) animatorSource.Hit -= OnHit;
    }

    void Start()
    {
        if (animatorSource == null)
            Debug.LogError($"[{nameof(ShapeAttack)}] no {nameof(UnitAnimator)} found — the attack will never land. Assign it (it's on the art child).", this);
    }

    // Fires when the animation reaches its hit frame (via UnitAnimator.Hit).
    void OnHit()
    {
        CombatWorld.Instance.Rebuild();
        if (shape == Shape.Circle)
            CombatWorld.Instance.Overlap(Centre, radius, Team, _hits);
        else
            CombatWorld.Instance.OverlapBox(Centre, Facing, size.x * 0.5f, size.y * 0.5f, Team, _hits);

        // Knockback pushes away from the ATTACKER, not from the shape's centre. On an offset rect the centre
        // sits out in front, so shoving outward from it would drive anything near the close edge backwards into
        // the attacker — the opposite of what a thrust does.
        Vector3 from = transform.position;
        float damage = _owner != null ? _owner.AttackPower : 0f;
        for (int i = 0; i < _hits.Count; i++)
        {
            var hit = _hits[i];
            hit.TakeDamage(damage, this);
            if (knockback > 0f)
            {
                Vector3 push = hit.Position - from;
                push.y = 0f;
                if (push.sqrMagnitude > 1e-6f) hit.ApplyKnockback(push.normalized * knockback);
            }
        }
    }

    // In edit mode this draws to the RIGHT, matching how the art is drawn (see Facing) — enough to judge reach
    // and offset against the sprite. It swings round to the unit's real facing as soon as the game runs.
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.3f, 0.6f);
        if (shape == Shape.Circle) DrawCircle(Centre, radius);
        else DrawRect(Centre, Facing, size.x * 0.5f, size.y * 0.5f);
    }

    static void DrawCircle(Vector3 c, float r)
    {
        const int seg = 28;
        Vector3 prev = c + new Vector3(r, 0, 0);
        for (int i = 1; i <= seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg;
            Vector3 next = c + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }

    static void DrawRect(Vector3 c, Vector3 forward, float halfWidth, float halfLength)
    {
        Vector3 fwd = forward;
        fwd.y = 0f;
        fwd = fwd.sqrMagnitude > 1e-6f ? fwd.normalized : Vector3.right;
        Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);

        Vector3 f = fwd * halfLength, r = right * halfWidth;
        Vector3 a = c - f - r, b = c + f - r, d = c + f + r, e = c - f + r;
        Gizmos.DrawLine(a, b);
        Gizmos.DrawLine(b, d);
        Gizmos.DrawLine(d, e);
        Gizmos.DrawLine(e, a);
    }
}
