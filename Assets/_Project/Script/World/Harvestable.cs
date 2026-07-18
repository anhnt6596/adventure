using UnityEngine;
using VContainer;

// A thing you hit to break (tree, rock, bush): HP like an enemy, in the combat world so attacks
// find it. On death it will drop resources (wood) — spawn wired later.
public class Harvestable : MonoBehaviour, IDamageable
{
    [SerializeField] float maxHp = 20f;
    [SerializeField] float hitRadius = 0.5f;
    [SerializeField] int team = 1;          // environment team; the player (team 0) can hit it

    float _hp;
    CombatWorld _combat;

    public Vector3 Position => transform.position;
    public float HitRadius => hitRadius;
    public bool IsAlive => _hp > 0f;
    public int Team => team;

    void Awake() => _hp = maxHp;

    // Injected when its map is instantiated through the container (MapService does this).
    [Inject]
    public void Construct(CombatWorld combat)
    {
        _combat = combat;
        _combat.Add(this);
    }

    void Start()
    {
        if (_combat == null)
            Debug.LogError($"[{nameof(Harvestable)}] not injected — its map must be instantiated through the DI container, or add it to GameScope's Auto Inject list.", this);
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
        // TODO: spawn wood pieces here, launched away from the attack's force origin (source).
        Debug.Log($"[Harvestable] {name} broken.");
        gameObject.SetActive(false);
    }

    // The hit circle sits at transform.position — this must line up with where the tree is drawn,
    // or an attack that visually connects will miss.
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.5f, 1f, 0.4f, 0.7f);
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
