// The stomach's fixed RULES. The numbers meant to grow with level and gear are not here — they are Stats on
// ICharacterStats (MaxHunger, HungerDrain, Regen). What is left is the shape of the mechanic rather than a
// character's numbers: where the well-fed line sits, and how hard the empty end bites.
//
// The split is not "one is important": it is which of them a character can be BETTER at. Anyone can be a
// faster healer; nobody is well fed at a different fullness, because that line is what the words mean.
public interface IHungerConfig
{
    // How full a character starts. A FRACTION, not an absolute: the stomach is a Stat that grows with
    // level and gear, and an absolute would quietly become a smaller and smaller share of it.
    float StartFullness { get; }

    float WellFedFraction { get; }  // above this fraction of fullness, HP regenerates

    // Fraction of MAX HP lost per second while empty, not a flat amount: starving should take the same
    // number of seconds to kill whatever the character's HP pool is. Of MAX and not of CURRENT — a share of
    // current HP only ever approaches zero, so it would never actually finish anyone off.
    //
    // "Share", not "percent", and the config types percent — whoever authors a number reads 1 as 1%, whoever
    // does the maths gets 0.01. A field called Percent holding 0.01 is a lie waiting to be multiplied by a
    // hundred twice or not at all. StatId.Regen keeps the same rule from the other side: the stat holds the
    // percent, and Hunger converts where it uses it.
    float StarveShare { get; }
}
