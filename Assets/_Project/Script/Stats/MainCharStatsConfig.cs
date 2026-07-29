using UnityEngine;

[CreateAssetMenu(menuName = "Stats/Main Char Stats")]
public class MainCharStatsConfig : Config, IInventoryConfig, IDamageableConfig
{
    public float moveSpeed = 6f;
    public float attackSpeed = 1f;
    public float attackPower = 5f;
    public float attackCooldown = 0f;     // recovery AFTER the swing ends, at 1x attack speed; one attack costs swing + this (0 = swing back-to-back)
    public float mass = 1f;             // how hard to shove aside in collisions (not physics)
    public float pickupRadius = 1.5f;   // how close a pickable must be for the character to grab it
    public int backpackCapacity = 20;   // total resources the character can carry

    [Header("Health")]
    public float maxHp = 100f;          // hit radius is NOT here — it's a field on the MC prefab's Damageable, authored against the art

    public int Capacity => backpackCapacity;   // IInventoryConfig
    public float MaxHp => maxHp;               // IDamageableConfig — the HP the MC's Damageable reads
    public int Team => 1;                       // player (Damageable actually takes team off MCController)
}
