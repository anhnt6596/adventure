using UnityEngine;

// Stats + death drops for one kind of prop (Oak, Rock, Chest...). Keyed by id in ConfigRegistry like
// EnemyConfig; a Prop resolves it by its own Id. One SO covers both concerns: IDamageableConfig
// (HP/hit-radius/team) for the Damageable, IDeathDropableConfig (loot) for the Dropable.
[CreateAssetMenu(menuName = "Config/Prop")]
public class PropConfig : Config, IDamageableConfig, IDeathDropableConfig
{
    [Header("Stats")]
    public float maxHp = 20f;
    public float hitRadius = 0.5f;
    public int team = 2;              // 0 = neutral (hits all, incl self), 1 = player, 2 = enemy/environment

    [Header("Drops — what it provides on death")]
    public DeathDrop[] drops;

    public float MaxHp => maxHp;
    public float HitRadius => hitRadius;
    public int Team => team;
    public DeathDrop[] Drops => drops;
}

// One kind of thing dropped on death, and how many (inclusive range). Wood, stone, gold — all the same.
// TODO(drops): `prefab` is a direct ref, so every drop prefab stays resident in RAM alongside the config.
// Later switch to an id/path loaded via Resources (load on demand, free when done) to save memory. The
// drop logic will likely grow too (tables, weights, conditions) — this struct is the seam for that.
[System.Serializable]
public struct DeathDrop
{
    public GameObject prefab;   // the pickup to spawn
    [Min(0)] public int min;
    [Min(0)] public int max;
}
