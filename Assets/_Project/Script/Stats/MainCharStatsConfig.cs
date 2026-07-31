using UnityEngine;

[CreateAssetMenu(menuName = "Stats/Main Char Stats")]
public class MainCharStatsConfig : Config, IHungerConfig, IDamageableConfig
{
    public float moveSpeed = 6f;
    public float attackSpeed = 1f;
    public float attackPower = 5f;
    public float attackCooldown = 0f;     // recovery AFTER the swing ends, at 1x attack speed; one attack costs swing + this (0 = swing back-to-back)
    public float mass = 1f;             // how hard to shove aside in collisions (not physics)
    public float pickupRadius = 1.5f;   // how close a pickable must be for the character to grab it

    [Header("Health")]
    public float maxHp = 100f;          // hit radius is NOT here — it's a field on the MC prefab's Damageable, authored against the art

    [Header("Hunger")]
    [Tooltip("BASE stomach size. Nothing carries food, so this is the whole food budget for a trip. " +
             "A Stat at runtime — level and gear will raise it.")]
    public float maxHunger = 100f;
    [Tooltip("BASE fullness lost per second. Slow and generous: the player should rarely look at the bar. " +
             "A Stat at runtime — gear and traits will move it either way.")]
    public float hungerDrain = 0.35f;
    [Tooltip("Fullness fraction above which HP regenerates. A rule of the mechanic, not a character number.")]
    [Range(0f, 1f)] public float wellFed = 0.75f;
    [Tooltip("HP per second regained while above the well-fed line.")]
    public float wellFedHeal = 1.5f;
    [Tooltip("HP per second lost while completely empty.")]
    public float starveDamage = 2f;

    public float WellFedFraction => wellFed;   // IHungerConfig
    public float WellFedHeal => wellFedHeal;
    public float StarveDamage => starveDamage;
    public float MaxHp => maxHp;               // IDamageableConfig — the HP the MC's Damageable reads
    public int Team => Teams.Player;            // (Damageable actually takes team off MCController)
}
