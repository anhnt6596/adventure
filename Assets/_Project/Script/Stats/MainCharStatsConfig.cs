using UnityEngine;

[CreateAssetMenu(menuName = "Stats/Main Char Stats")]
public class MainCharStatsConfig : Config, IHungerConfig, IDamageableConfig
{
    public float moveSpeed = 6f;
    public float attackSpeed = 1f;
    public float attackPower = 5f;
    // THE REST AFTER A BLOW, on top of the swing itself, at 1x attack speed — one attack costs its own
    // animation plus this. Attacks only: a skill's wait is the skill's own number, on the prefab that carries
    // it.
    public float recovery = 0.01f;
    public float mass = 1f;             // how hard to shove aside in collisions (not physics)
    public float pickupRadius = 1.5f;   // how close a pickable must be for the character to grab it
    // Vision has no field here on purpose — see MainCharStats. Its baseline is the spotlight drawn on the
    // character's own prefab, so the only value this could ever hold is 1.

    [Header("Health")]
    public float maxHp = 100f;          // hit radius is NOT here — it's a field on the MC prefab's Damageable, authored against the art

    [Header("Hunger")]
    [Tooltip("BASE stomach size. Nothing carries food, so this is the whole food budget for a trip. " +
             "A Stat at runtime — level and gear will raise it.")]
    public float maxHunger = 100f;
    [Tooltip("BASE fullness lost per second AT LEVEL 1. Slow and generous: the player should rarely look at " +
             "the bar. A Stat at runtime — gear and traits will move it either way.")]
    public float hungerDrain = 0.35f;
    [Tooltip("How much faster the stomach empties per level, COMPOUNDING. 0.01 = +1% a level, which is 2.7x " +
             "the drain at level 100. Careful: 3% here is 18x, not 'a few percent'.")]
    [Min(0f)] public float hungerDrainPerLevel = 0.01f;
    // Sliders, because every value from 0 to 1 is a sensible answer: both are a POSITION on the bar.
    [Tooltip("How full the character starts, as a fraction of the stomach.")]
    [Range(0f, 1f)] public float startFullness = 0.5f;
    [Tooltip("Fullness fraction above which HP regenerates. A rule of the mechanic, not a character number.")]
    [Range(0f, 1f)] public float wellFed = 0.75f;

    // Plain numbers, like every other rate in this file. They are fractions of max HP, but a slider over
    // 0..1 would be useless on them: 1.0 means the whole HP pool every second, so everything usable lives in
    // the first percent of the track. Being a fraction is not what earns a slider - the whole span being
    // meaningful is.
    [Tooltip("BASE percent of max HP regained per second while above the well-fed line. Type 0.75 for " +
             "0.75%/s. A Stat at runtime (StatId.Regen) — upgrades and gear move it.")]
    [Min(0f)] public float wellFedHealPercent = 0.75f;
    [Tooltip("PERCENT of max HP lost per second while completely empty. Type 1 for 1%/s.")]
    [Min(0f)] public float starvePercent = 1f;

    // The drain a character of this kind has AT a level, and the only place the curve is written. A BASE, not a
    // modifier: levelling up changes what the character IS, so it moves Stat.BaseValue and leaves the modifier
    // list to gear and buffs. That also keeps Hunger.DrainPaused absolute — it pauses with a -100% Mul, which
    // zeroes any base at all, where a level modifier added into the same sum would have left part of the drain
    // running (the warning DrainPaused already carries).
    //
    // COMPOUNDING, chosen against the level ceiling the way CharacterLevels.Growth is: 99 steps is a long lever,
    // so the per-level number has to be small to land somewhere sane at the top. See the tooltip.
    public float HungerDrainAt(int level)
        => hungerDrain * Mathf.Pow(1f + hungerDrainPerLevel, Mathf.Max(0, level - CharacterLevels.StartLevel));

    public float StartFullness => startFullness;   // IHungerConfig
    public float WellFedFraction => wellFed;
    public float StarveShare => starvePercent * 0.01f;   // percent in, share out
    public float MaxHp => maxHp;               // IDamageableConfig — the HP the MC's Damageable reads
    public int Team => Teams.Player;            // (Damageable actually takes team off MCController)
}
