// The three ways something can move a stat. Named for where they land in the formula, because that is the
// only thing the call site needs to know:
//
//     final = (base * Mul + Add) * FinalMul
//
// MUL AND FINALMUL ARE BOTH PRODUCTS, and the difference is what they reach. Mul scales the BASE only, so a
// flat +20 added afterwards escapes it; FinalMul scales everything, that +20 included. That is the whole
// reason there are two: "+50% of what this character is" and "+50% of everything, however you got it" are
// different promises, and a game that only has one of them ends up faking the other.
public enum StatModKind
{
    Add,        // summed together. No modifiers of this kind = 0
    Mul,        // multiplied together. No modifiers of this kind = 1
    FinalMul,   // multiplied together, applied last. No modifiers of this kind = 1
}

// One modifier on one stat.
//
// VALUE IS THE MULTIPLIER ITSELF for Mul and FinalMul — 1.5 means x1.5, not "+50%". They are multiplied
// together, so the neutral value is 1 and typing 0.5 is a request to HALVE the stat. Two 1.5s make x2.25
// rather than x2: multiplicative stacking grows faster than it looks, which is worth knowing before pricing
// anything in it.
public class StatModifier
{
    public readonly float Value;
    public readonly StatModKind Kind;

    // Whatever put it there. This is how it comes off again without anybody keeping a handle: the upgrade
    // tree removes everything it added by passing itself, and a buff that expires does the same.
    public readonly object Source;

    public StatModifier(float value, StatModKind kind, object source = null)
    {
        Value = value;
        Kind = kind;
        Source = source;
    }
}
