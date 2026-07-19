using UnityEngine;
using VContainer;

// Anything with HP that can be hit and dies. HP/team/hit-radius come from an IDamageableConfig. What it
// PROVIDES on death is a separate concern: Damageable just fires Died, and a DeathDropable (or anything
// else — sound, XP, break FX) listens. Lives in the combat world so attacks find it.
public class Damageable : MonoBehaviour, IDamageable
{
    // TEMP: serialized concrete SO so it can be dragged in the editor (Unity can't serialize an
    // interface). Later assign by code (config provider keyed by id) and depend only on IDamageableConfig.
    [SerializeField] DamageableConfig config;

    IDamageableConfig Cfg => config;

    float _hp;
    CombatWorld _combat;

    public Vector3 Position => transform.position;
    public float HitRadius => config != null ? Cfg.HitRadius : 0.5f;
    public bool IsAlive => _hp > 0f;
    public int Team => config != null ? Cfg.Team : 2;

    public event System.Action Damaged;        // non-fatal hit — views like HitFlash listen
    public event System.Action<object> Died;   // killed — the arg is the damage source (force origin for drops)

    void Awake()
    {
        if (config != null) _hp = Cfg.MaxHp;
    }

    // Injected when its map is instantiated through the container (MapService does this).
    [Inject]
    public void Construct(CombatWorld combat)
    {
        _combat = combat;
        _combat.Add(this);
    }

    void Start()
    {
        if (config == null)
            Debug.LogError($"[{nameof(Damageable)}] no DamageableConfig assigned — drag the kind's SO; it has no HP.", this);
        if (_combat == null)
            Debug.LogError($"[{nameof(Damageable)}] not injected — its map must be instantiated through the DI container, or add it to GameScope's Auto Inject list.", this);
    }

    void OnDestroy() => _combat?.Remove(this);

    public void TakeDamage(float amount, object source)
    {
        if (!IsAlive) return;
        _hp -= amount;
        if (_hp <= 0f) { Die(source); return; }
        Damaged?.Invoke();
    }

    void Die(object source)
    {
        _combat?.Remove(this);
        Died?.Invoke(source);          // DeathDropable (and anything else) reacts before we vanish
        gameObject.SetActive(false);
    }

    // The hit circle sits at transform.position — this must line up with where the thing is drawn,
    // or an attack that visually connects will miss.
    void OnDrawGizmos()
    {
        float r = config != null ? Cfg.HitRadius : 0.5f;
        Gizmos.color = new Color(0.5f, 1f, 0.4f, 0.7f);
        const int seg = 24;
        Vector3 c = transform.position;
        Vector3 prev = c + new Vector3(r, 0, 0);
        for (int i = 1; i <= seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg;
            Vector3 next = c + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}
