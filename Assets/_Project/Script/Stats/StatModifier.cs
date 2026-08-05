// The three ways something can move a stat. Named for where they land in the formula, because that is the
// only thing the call site needs to know:
//
//     final = (base * (1 + Mul) + Add) * (1 + FinalMul)
//
// MUL AND FINALMUL ARE BOTH SHARES THAT ADD UP (see below), and the difference between them is what they
// reach. Mul is a share of the BASE only, so a flat +20 added afterwards escapes it; FinalMul is a share of
// everything, that +20 included. That is the whole reason there are two: "+50% of what this character is" and
// "+50% of everything, however you got it" are different promises, and a game that only has one of them ends
// up faking the other.
public enum StatModKind
{
    Add,        // flat, summed. No modifiers of this kind = 0
    Mul,        // a share of the base, summed. No modifiers of this kind = 0
    FinalMul,   // a share of the lot, summed, applied last. No modifiers of this kind = 0
}

// One modifier on one stat.
//
// VALUE IS WHAT IT ADDS, whichever kind it is. On Mul and FinalMul that makes it a SHARE and not a factor:
// 0.1 is +10%, -0.5 takes half away, and 0 is a modifier that does nothing at all — it is not x0.
//
// THEY STACK BY ADDING: two +50% come to +100%, not +125%. That is the arithmetic a player can do in their
// head, and it prices in a straight line instead of running away as a tree gets deeper.
//
// What it costs: enough negative ones take the share below -1 and the stat past zero into negative, which a
// product could never do. Nothing is clamped here, because where a floor belongs is a property of the stat
// and not of this.
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
