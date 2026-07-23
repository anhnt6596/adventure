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
    bool _inWorld;         // guards against a double Add across the inject/OnEnable ordering

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

    // Injected when its map is instantiated through the container (MapService does this). Inject lands
    // AFTER the first OnEnable (which had no world yet), so join here too — but only while active, so a
    // prefab object that ships disabled stays out of the world until it's actually switched on.
    [Inject]
    public void Construct(CombatWorld combat)
    {
        _combat = combat;
        if (isActiveAndEnabled) JoinWorld();
    }

    void Start()
    {
        if (config == null)
            Debug.LogError($"[{nameof(Damageable)}] no DamageableConfig assigned — drag the kind's SO; it has no HP.", this);
        if (_combat == null)
            Debug.LogError($"[{nameof(Damageable)}] not injected — its map must be instantiated through the DI container, or add it to GameScope's Auto Inject list.", this);
    }

    // Only in the combat world while enabled: a disabled object can't be hit, and a re-enabled one rejoins.
    void OnEnable() => JoinWorld();          // _combat is still null on the very first call (before inject)
    void OnDisable() => LeaveWorld();
    void OnDestroy() => LeaveWorld();

    void JoinWorld()  { if (_combat != null && !_inWorld) { _combat.Add(this); _inWorld = true; } }
    void LeaveWorld() { if (_combat != null && _inWorld)  { _combat.Remove(this); _inWorld = false; } }

    public void TakeDamage(float amount, object source)
    {
        if (!IsAlive) return;
        _hp -= amount;
        if (_hp <= 0f) { Die(source); return; }
        Damaged?.Invoke();
    }

    void Die(object source)
    {
        LeaveWorld();                  // out of the world before it can be re-targeted
        Died?.Invoke(source);          // DeathDropable (and anything else) reacts before we vanish
        gameObject.SetActive(false);   // OnDisable's LeaveWorld is then a no-op
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
