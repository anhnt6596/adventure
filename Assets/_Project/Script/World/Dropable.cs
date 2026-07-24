using Lean.Pool;
using UnityEngine;

// Spawns what this provides and flings it out. Call Drop(source) whenever the moment comes — a death, a
// chest opening, a node depleting, a timer. It knows HOW to drop, not WHY, so any trigger can drive it,
// and it needs no Damageable (things that drop without ever taking damage still work).
[DisallowMultipleComponent]
public class Dropable : MonoBehaviour
{
    // TEMP: serialized concrete SO so it can be dragged in the editor (Unity can't serialize an
    // interface). Later assign by code and depend only on IDeathDropableConfig.
    [SerializeField] DamageableConfig config;

    IDeathDropableConfig Cfg => config;

    void Start()
    {
        if (config == null)
            Debug.LogError($"[{nameof(Dropable)}] no config assigned — drag a config that has a drop list.", this);
    }

    // source = what the loot flies away from (e.g. the attacker). Null → no bias, pieces fan out randomly.
    public void Drop(object source)
    {
        if (config == null || Cfg.Drops == null) return;

        Vector3 origin = source is Component c ? c.transform.position : transform.position;
        Vector3 flingDir = transform.position - origin;   // source -> this
        flingDir.y = 0f;

        foreach (var drop in Cfg.Drops)
        {
            if (drop.prefab == null) continue;
            int count = Random.Range(drop.min, drop.max + 1);
            for (int i = 0; i < count; i++)
            {
                var obj = LeanPool.Spawn(drop.prefab, transform.position, Quaternion.identity);
                if (obj.TryGetComponent<FlyingPickup>(out var fly))
                    fly.Launch(flingDir);
            }
        }
    }
}
