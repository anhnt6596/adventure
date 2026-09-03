using System.Collections.Generic;
using UnityEngine;

// An instant area blow: it plays its clip, and on that clip's hit frame everything inside a shape takes damage.
// The shape is either a CIRCLE (a swing that lands all around the owner) or a RECT (a thrust or cleave that
// reaches down a lane in front of it) — same trigger, same damage, same knockback either way, so a new weapon is
// a field change rather than a new class. Reach and shape belong to THIS weapon, not to a character stat.
//
// It is an ABILITY, so it has no button of its own: a SingleAttack in the Attack slot throws it for a character
// whose attack is one blow, a ComboAttack throws it as one step of a string, and an enemy's AI throws it
// directly. Whoever asks, it is the same Swing().
//
// The rect is oriented by the owner's FacingDir, NOT by any transform: a billboard unit never rotates its
// transform, it only tracks a facing.
public class ShapeAttack : AttackAbility
{
    public enum Shape { Circle, Rect }

    [SerializeField] Shape shape = Shape.Circle;
    [SerializeField] float radius = 1.5f;                     // Circle: reach in every direction
    [SerializeField] Vector2 size = new Vector2(1.5f, 3f);    // Rect: x = width ACROSS the facing, y = length ALONG it
    [SerializeField] float forwardOffset = 0f;                // slide the shape this far along the facing; 0 = centred on the owner
    [SerializeField] float knockback = 5f;                    // shove dealt outward from the owner; 0 = none

    [Tooltip("Swat shots out of the air: anything hostile and blockable standing in the same shape is taken " +
             "out along with whatever was hit. What counts as blockable is the shot's own business — one that " +
             "spends itself on a body spends itself on a blade, one that carries through a rank carries " +
             "through this. A tick because most blows are not a guard: a sword that erased every arrow it " +
             "happened to swing past would make standing still the answer to being shot at.")]
    [SerializeField] bool blocksProjectiles;

    int Team => Owner != null ? Owner.Team : Teams.Universal;   // ownerless -> belongs to no side, so it hits everything
    readonly List<IDamageable> _hits = new List<IDamageable>();

    // Anchored on THIS object's position — put the component wherever the blow should come from. There is
    // deliberately no anchor-Transform field: a transform offset is world-axis, and a billboard unit never
    // rotates its transform, so any non-zero one would pin the hitbox to a compass direction instead of to the
    // unit's facing. Facing-relative placement is what forwardOffset is for.
    // The fallback is +X (east), not +Z (north): it is only ever reached in edit mode, where Awake hasn't run
    // and there is no owner to have a facing. The 2D art is authored facing RIGHT, so drawing the box to the
    // right is the one orientation you can judge straight against the sprite sitting in front of you.
    protected Vector3 Facing => Owner != null ? Owner.FacingDir : Vector3.right;
    Vector3 Centre => transform.position + Facing * forwardOffset;

    protected override void Land()
    {
        // ON THE HIT FRAME, with the bodies, and out of the same shape. A guard that ran on its own timing
        // would be a second window the player has to learn, when what they are reading is one swing.
        if (blocksProjectiles)
        {
            if (shape == Shape.Circle) Projectile.CancelIn(Centre, radius, Team);
            else Projectile.CancelIn(Centre, Facing, size.x * 0.5f, size.y * 0.5f, Team);
        }

        CombatWorld.Instance.Rebuild();
        if (shape == Shape.Circle)
            CombatWorld.Instance.Overlap(Centre, radius, Team, _hits);
        else
            CombatWorld.Instance.OverlapBox(Centre, Facing, size.x * 0.5f, size.y * 0.5f, Team, _hits);

        // Knockback pushes away from the ATTACKER, not from the shape's centre. On an offset rect the centre
        // sits out in front, so shoving outward from it would drive anything near the close edge backwards into
        // the attacker — the opposite of what a thrust does.
        Vector3 from = transform.position;
        // Rolled once for the swing, not once per body: a blow either landed well or it did not, and a
        // cleave that crit on the left half of a crowd is not a thing anybody could read.
        float damage = Owner != null ? Owner.RollAttackDamage(out _) : 0f;
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
