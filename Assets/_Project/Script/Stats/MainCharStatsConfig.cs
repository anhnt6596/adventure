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
    // A slider, because every value from 0 to 1 is a sensible answer: it is a POSITION on the fullness bar.
    [Tooltip("Fullness fraction above which HP regenerates. A rule of the mechanic, not a character number.")]
    [Range(0f, 1f)] public float wellFed = 0.75f;

    // Plain numbers, like every other rate in this file. They are fractions of max HP, but a slider over
    // 0..1 would be useless on them: 1.0 means the whole HP pool every second, so everything usable lives in
    // the first percent of the track. Being a fraction is not what earns a slider - the whole span being
    // meaningful is.
    [Tooltip("PERCENT of max HP regained per second while above the well-fed line. Type 0.75 for 0.75%/s.")]
    [Min(0f)] public float wellFedHealPercent = 0.75f;
    [Tooltip("PERCENT of max HP lost per second while completely empty. Type 1 for 1%/s.")]
    [Min(0f)] public float starvePercent = 1f;

    public float WellFedFraction => wellFed;   // IHungerConfig
    public float WellFedHealShare => wellFedHealPercent * 0.01f;   // percent in, share out
    public float StarveShare => starvePercent * 0.01f;
    public float MaxHp => maxHp;               // IDamageableConfig — the HP the MC's Damageable reads
    public int Team => Teams.Player;            // (Damageable actually takes team off MCController)
}
