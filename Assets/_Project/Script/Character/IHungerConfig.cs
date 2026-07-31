// The stomach's fixed RULES. The two numbers that are meant to grow with level and gear are not here —
// they are Stats on ICharacterStats (MaxHunger, HungerDrain). What is left is the shape of the mechanic
// rather than a character's numbers: where the well-fed line sits, and how hard each end bites.
public interface IHungerConfig
{
    float WellFedFraction { get; }  // above this fraction of fullness, HP regenerates
    // Both of these are a SHARE OF MAX HP per second (0..1), not a flat amount, so neither the reward for
    // eating nor the price of starving changes meaning when the HP pool grows. Of MAX and not of CURRENT:
    // a share of current HP would never finish anyone off on the way down, and would crawl on the way up.
    //
    // "Share", not "percent", and the config types percent - whoever authors a number reads 0.75 as 0.75%,
    // whoever does the maths gets 0.0075. A field called Percent holding 0.0075 is a lie waiting to be
    // multiplied by a hundred twice or not at all.
    float WellFedHealShare { get; }
    // Fraction of MAX HP lost per second while empty, not a flat amount: starving should take the same
    // number of seconds to kill whatever the character's HP pool is. Of MAX and not of CURRENT — a share of
    // current HP only ever approaches zero, so it would never actually finish anyone off.
    float StarveShare { get; }
}
