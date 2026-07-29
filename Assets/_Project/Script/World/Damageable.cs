using UnityEngine;

// Anything with HP that can be hit and dies. HP and team come off the Unit it sits on — a Prop resolves its
// PropConfig by id, an enemy carries its EnemyConfig. The hit radius does NOT: it is authored right here on the
// prefab, because it is a property of this BODY, not of the kind. Two things sharing a config can be drawn at
// different sizes, and the radius has to match what you see or an attack that visually connects will miss —
// which you can only judge against the art, in the prefab, next to the gizmo. What it PROVIDES on death is a
// separate concern: Damageable just fires Died, and a DropOnDeath (or anything else — sound, XP, break FX)
// listens. Lives in the combat world so attacks find it.
public class Damageable : MonoBehaviour, IDamageable
{
    [SerializeField] float hitRadius = 0.5f;   // body size for being hit — the red gizmo circle. Attack REACH is a different thing; it lives on the attack component (ShapeAttack.radius).

    Unit _unit;                    // the unit this belongs to — supplies config + team
    CollisionBody _body;           // the physics body a knockback shoves (null = can't be shoved)

    IDamageableConfig Cfg => _unit != null ? _unit.DamageableConfig : null;

    float _hp;
    bool _inWorld;                 // guards against a double Add/Remove

    public Vector3 Position => transform.position;
    public float HitRadius => hitRadius;
    public bool IsAlive => _hp > 0f;
    public int Team => _unit != null ? _unit.Team : 2;

    public float Hp => _hp;
    public float MaxHp => Cfg != null ? Cfg.MaxHp : 0f;

    public event System.Action<object> Damaged;   // non-fatal hit; arg = damage source (HitFlash ignores it, AI targets it)
    public event System.Action<object> Died;   // killed — the arg is the damage source (force origin for drops)
    public event System.Action HealthChanged;   // any HP change (spawn init, damage, death) — the HUD health bar listens

    void Awake()
    {
        _unit = GetComponentInParent<Unit>();
        _body = GetComponentInParent<CollisionBody>();
        if (_body == null) _body = GetComponentInChildren<CollisionBody>(true);
    }

    // Forward the attack's shove to the body; mass-scaling (and mass 0 = immovable) lives in AddImpulse.
    public void ApplyKnockback(Vector3 impulse)
    {
        if (_body != null) _body.AddImpulse(impulse);
    }

    // HP is read in Start, not Awake: a unit resolves its config during injection (Construct runs after Awake),
    // so MaxHp isn't available until now.
    void Start()
    {
        if (Cfg == null)
        {
            Debug.LogError($"[{nameof(Damageable)}] no config on its Unit ({(_unit != null ? _unit.GetType().Name : "no Unit found")}) — it has no HP.", this);
            return;
        }
        _hp = Cfg.MaxHp;
        HealthChanged?.Invoke();
    }

    // In the combat world only while enabled: a disabled object can't be hit, a re-enabled one rejoins. The
    // world is a static (CombatWorld.Instance), ready before any OnEnable, so it self-registers — no wiring.
    void OnEnable() => JoinWorld();
    void OnDisable() => LeaveWorld();
    void OnDestroy() => LeaveWorld();

    void JoinWorld()  { if (_inWorld) return; CombatWorld.Instance.Add(this); _inWorld = true; }
    void LeaveWorld() { if (!_inWorld) return; CombatWorld.Instance.Remove(this); _inWorld = false; }

    public void TakeDamage(float amount, object source)
    {
        if (!IsAlive) return;
        _hp -= amount;
        HealthChanged?.Invoke();
        if (_hp <= 0f) { Die(source); return; }
        Damaged?.Invoke(source);
    }

    void Die(object source)
    {
        LeaveWorld();                  // out of the world before it can be re-targeted
        Died?.Invoke(source);          // DropOnDeath (and anything else) reacts before we vanish
        gameObject.SetActive(false);   // OnDisable's LeaveWorld is then a no-op
    }

    // The hit circle sits at transform.position — this must line up with where the thing is drawn, or an attack
    // that visually connects will miss. Drawn on the ground plane (XZ), not as a sphere: the hit test is a
    // 2D circle. Now that the radius is a field on this component it reads the AUTHORED value in edit mode —
    // it used to fall back to a flat 0.5 with no config around, which made it useless for the one job it has.
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.25f, 0.25f, 0.9f);
        const int seg = 24;
        Vector3 c = transform.position;
        Vector3 prev = c + new Vector3(hitRadius, 0, 0);
        for (int i = 1; i <= seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg;
            Vector3 next = c + new Vector3(Mathf.Cos(a) * hitRadius, 0, Mathf.Sin(a) * hitRadius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}
