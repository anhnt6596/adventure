using Lean.Pool;
using UnityEngine;

// Spawns what a unit provides on death and flings it out. Call Drop(source) whenever the moment comes — a
// death, a chest opening, a node depleting. It knows HOW to drop, not WHY. The drop list comes off the unit's
// config (a Prop's PropConfig is also an IDeathDropableConfig), so nothing is dragged onto the prefab.
[DisallowMultipleComponent]
public class Dropable : MonoBehaviour
{
    Unit _unit;
    IDeathDropableConfig Cfg => _unit != null ? _unit.DamageableConfig as IDeathDropableConfig : null;

    void Awake() => _unit = GetComponentInParent<Unit>();

    void Start()
    {
        if (Cfg == null)
            Debug.LogError($"[{nameof(Dropable)}] no drop config on its Unit — needs a config with a drop list.", this);
    }

    // source = what the loot flies away from (e.g. the attacker). Null → no bias, pieces fan out randomly.
    public void Drop(object source)
    {
        var cfg = Cfg;
        if (cfg == null || cfg.Drops == null) return;

        Vector3 origin = source is Component c ? c.transform.position : transform.position;
        Vector3 flingDir = transform.position - origin;   // source -> this
        flingDir.y = 0f;

        foreach (var drop in cfg.Drops)
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
