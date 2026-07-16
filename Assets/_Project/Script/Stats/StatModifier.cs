public enum StatModType { Flat, PercentAdd, PercentMult }

public class StatModifier
{
    public readonly float Value;
    public readonly StatModType Type;
    public readonly object Source;

    public StatModifier(float value, StatModType type, object source = null)
    {
        Value = value;
        Type = type;
        Source = source;
    }
}
