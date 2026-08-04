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
    float _lastMax;                // to tell a raised ceiling from a lowered one when the stat moves
    bool _inWorld;                 // guards against a double Add/Remove

    public Vector3 Position => transform.position;
    public float HitRadius => hitRadius;
    public bool IsAlive => _hp > 0f;
    // No Unit means nobody declared a side, so take the one that is loudly wrong rather than quietly wrong:
    // Universal is hittable by everyone. The old fallback was the enemy team, which under the new scheme would
    // have made a stray Damageable join the peaceful animals and get hunted by predators.
    public int Team => _unit != null ? _unit.Team : Teams.Universal;

    public float Hp => _hp;

    // A live maximum when the unit has one (the main character), the config's flat number otherwise (props,
    // enemies). One property so nothing downstream has to know which kind of body it is looking at.
    IStat MaxStat => _unit != null ? _unit.MaxHpStat : null;
    public float MaxHp => MaxStat != null ? MaxStat.Value : (Cfg != null ? Cfg.MaxHp : 0f);

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
        if (Cfg == null && MaxStat == null)
        {
            Debug.LogError($"[{nameof(Damageable)}] no config on its Unit ({(_unit != null ? _unit.GetType().Name : "no Unit found")}) — it has no HP.", this);
            return;
        }

        _hp = MaxHp;
        _lastMax = _hp;

        // Only a live maximum can move, so only that one is worth listening to.
        if (MaxStat != null) MaxStat.Changed += OnMaxChanged;

        HealthChanged?.Invoke();
    }

    // Raising the ceiling hands over the difference, so buying +20 max HP is worth 20 HP right now rather
    // than being a promise that only pays off after the next heal. Lowering it leaves the current HP alone
    // and just clamps — a debuff that takes the ceiling away should not also deal the damage.
    void OnMaxChanged()
    {
        float max = MaxHp;
        float delta = max - _lastMax;
        _lastMax = max;

        if (!IsAlive) return;   // nothing to top up on something already dead

        if (delta > 0f) _hp += delta;
        else if (_hp > max) _hp = max;
        else return;            // ceiling moved but this body was not touching it

        HealthChanged?.Invoke();
    }

    // In the combat world only while enabled: a disabled object can't be hit, a re-enabled one rejoins. The
    // world is a static (CombatWorld.Instance), ready before any OnEnable, so it self-registers — no wiring.
    void OnEnable() => JoinWorld();
    void OnDisable() => LeaveWorld();

    void OnDestroy()
    {
        LeaveWorld();
        // Stats outlive a body — a character switch throws the body away and builds new stats, but a timed
        // buff on the old set could still fire — so the listener has to come off with the body.
        if (MaxStat != null) MaxStat.Changed -= OnMaxChanged;
    }

    void JoinWorld()  { if (_inWorld) return; CombatWorld.Instance.Add(this); _inWorld = true; }
    void LeaveWorld() { if (!_inWorld) return; CombatWorld.Instance.Remove(this); _inWorld = false; }

    // Nothing lands while this is on — no HP lost, and no Damaged either, so it does not even provoke the
    // thing that swung. A real concept rather than a cheat-only escape hatch: i-frames after a hit, and a
    // scripted invulnerable phase, both want exactly this.
    public bool Invulnerable { get; set; }

    public void TakeDamage(float amount, object source)
    {
        if (!IsAlive || Invulnerable) return;
        _hp -= amount;
        HealthChanged?.Invoke();
        if (_hp <= 0f) { Die(source); return; }
        Damaged?.Invoke(source);
    }

    // Put HP back, never above the maximum and never onto something already dead. Kept apart from
    // TakeDamage(-x) on purpose: that path fires Damaged and can trip Died, and healing is neither.
    public void Heal(float amount)
    {
        if (!IsAlive || amount <= 0f) return;

        float healed = Mathf.Min(amount, MaxHp - _hp);
        if (healed <= 0f) return;

        _hp += healed;
        HealthChanged?.Invoke();
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
