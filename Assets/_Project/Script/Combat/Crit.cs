// How crit points become a crit chance.
//
// POINTS, NOT A PERCENTAGE, and that is the whole reason this file exists. A percentage has a ceiling, so
// every source of it has to be balanced against how much of the ceiling it eats: the last +10% is worth far
// more than the first, stacking two sources can overshoot 100 and waste the difference, and a node that
// promises +10% is a lie once you already have 95. Points have no ceiling, so a node is worth the same number
// wherever it lands on the pile — the CHANCE flattens instead of the reward.
//
//     chance = points / (points + HalfChance)
//
//     0 points   -> 0%
//     100        -> 50%     (HalfChance is exactly the "half of the way there" mark)
//     300        -> 75%
//     900        -> 90%
//     infinity   -> 100%, never reached
//
// Same shape League uses for ability haste, and picked for the same reason: it makes "more" always mean more
// without ever meaning enough.
//
// HalfChance IS THE ONLY KNOB, and it is a constant rather than a config field because it is not a balance
// number — it is the unit points are counted in. Change it and every crit number in the game means something
// different at once; that is a decision about the currency, not a tuning pass.
public static class Crit
{
    public const float HalfChancePoints = 100f;

    public static float ChanceFrom(float points)
        => points <= 0f ? 0f : points / (points + HalfChancePoints);
}
