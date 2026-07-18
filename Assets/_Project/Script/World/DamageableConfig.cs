using UnityEngine;

// Shared stats + death drops for one kind of damageable (OakConfig, RockConfig, SlimeConfig...).
// Dragged straight onto each Damageable — no DI, because its identity is which SO it points at.
[CreateAssetMenu(menuName = "Combat/Damageable")]
public class DamageableConfig : ScriptableObject
{
    [Header("Stats")]
    public float maxHp = 20f;
    public float hitRadius = 0.5f;
    public int team = 2;              // 0 = neutral (hits all, incl self), 1 = player, 2 = enemy/environment

    [Header("Drops — what it provides on death")]
    public DeathDrop[] drops;
}

// One kind of thing dropped on death, and how many (inclusive range). Wood, stone, gold — all the same.
[System.Serializable]
public struct DeathDrop
{
    public GameObject prefab;   // the pickup to spawn
    [Min(0)] public int min;
    [Min(0)] public int max;
}
