using UnityEngine;
using VContainer;

// Anything with HP that can be hit and, when it dies, provides something: a tree drops wood, a rock
// drops stone, a monster drops gold. Stats + drops come from a shared DamageableConfig, so every one
// of a kind is tuned from one asset. Enemies later reuse this and add movement/AI on top.
public class Damageable : MonoBehaviour, IDamageable
{
    [SerializeField] DamageableConfig config;   // drag the kind's SO (OakConfig, RockConfig, SlimeConfig, ...)

    float _hp;
    CombatWorld _combat;

    public Vector3 Position => transform.position;
    public float HitRadius => config != null ? config.hitRadius : 0.5f;
    public bool IsAlive => _hp > 0f;
    public int Team => config != null ? config.team : 2;

    void Awake()
    {
        if (config != null) _hp = config.maxHp;
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
            Debug.LogError($"[{nameof(Damageable)}] no DamageableConfig assigned — drag the kind's SO; it has no HP and drops nothing.", this);
        if (_combat == null)
            Debug.LogError($"[{nameof(Damageable)}] not injected — its map must be instantiated through the DI container, or add it to GameScope's Auto Inject list.", this);
    }

    void OnDestroy() => _combat?.Remove(this);

    public void TakeDamage(float amount, object source)
    {
        if (!IsAlive) return;
        _hp -= amount;
        if (_hp <= 0f) Die();
    }

    void Die()
    {
        _combat?.Remove(this);
        SpawnDrops();
        gameObject.SetActive(false);
    }

    // Spawns what this provides on death. TODO(gỗ-văng): launch each piece away from the attack's
    // force origin instead of dropping it in place.
    void SpawnDrops()
    {
        if (config == null || config.drops == null) return;
        foreach (var drop in config.drops)
        {
            if (drop.prefab == null) continue;
            int count = Random.Range(drop.min, drop.max + 1);
            for (int i = 0; i < count; i++)
                Instantiate(drop.prefab, transform.position, Quaternion.identity);
        }
    }

    // The hit circle sits at transform.position — this must line up with where the thing is drawn,
    // or an attack that visually connects will miss.
    void OnDrawGizmos()
    {
        float r = config != null ? config.hitRadius : 0.5f;
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
