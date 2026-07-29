using UnityEngine;

[CreateAssetMenu(menuName = "Stats/Main Char Stats")]
public class MainCharStatsConfig : Config, IInventoryConfig, IDamageableConfig
{
    public float moveSpeed = 6f;
    public float attackSpeed = 1f;
    public float attackPower = 5f;
    public float attackDuration = 0.4f;   // lock while the swing plays, at 1x attack speed — match it to the attack clip's length
    public float attackCooldown = 0f;     // seconds between attack starts, at 1x attack speed; floored at attackDuration (0 = swing back-to-back)
    public float mass = 1f;             // how hard to shove aside in collisions (not physics)
    public float pickupRadius = 1.5f;   // how close a pickable must be for the character to grab it
    public int backpackCapacity = 20;   // total resources the character can carry

    [Header("Health")]
    public float maxHp = 100f;
    public float hitRadius = 0.5f;      // body size for being hit

    public int Capacity => backpackCapacity;   // IInventoryConfig
    public float MaxHp => maxHp;               // IDamageableConfig — HP/hit-radius the MC's Damageable reads
    public float HitRadius => hitRadius;
    public int Team => 1;                       // player (Damageable actually takes team off MCController)
}
