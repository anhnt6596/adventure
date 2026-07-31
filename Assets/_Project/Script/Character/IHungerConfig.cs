// The stomach's fixed RULES. The two numbers that are meant to grow with level and gear are not here —
// they are Stats on ICharacterStats (MaxHunger, HungerDrain). What is left is the shape of the mechanic
// rather than a character's numbers: where the well-fed line sits, and how hard each end bites.
public interface IHungerConfig
{
    float WellFedFraction { get; }  // above this fraction of fullness, HP regenerates
    float WellFedHeal { get; }      // HP per second while well fed
    float StarveDamage { get; }     // HP per second while empty
}
