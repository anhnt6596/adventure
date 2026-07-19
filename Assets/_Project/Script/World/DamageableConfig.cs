using UnityEngine;

// Shared stats + death drops for one kind of thing (OakConfig, RockConfig, SlimeConfig...). One SO
// satisfies both concerns: IDamageableConfig (HP/team) for Damageable, IDeathDropableConfig (loot) for
// DeathDropable. Dragged straight onto each — no DI, because its identity is which SO it points at.
[CreateAssetMenu(menuName = "Combat/Damageable")]
public class DamageableConfig : ScriptableObject, IDamageableConfig, IDeathDropableConfig
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
