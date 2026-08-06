// What killing a thing is worth. Separate from IDamageableConfig and IDeathDropableConfig for the same reason
// those are separate from each other: a config satisfies the concerns it actually has.
//
// PropConfig deliberately does NOT implement this, and that is where "trees pay no experience" is enforced —
// not in a rule somewhere that has to remember which is which. A tree is an infinite, riskless supply you can
// stand in front of all day; paying for it would make the axe the fastest way to level and the level bar a
// wood counter. It already pays in wood.
public interface IDeathExpConfig
{
    int Exp { get; }            // every kill, scaled to how dangerous the KIND is
    int FirstKillExp { get; }   // once, the first time this kind is killed at all — 0 = not a discovery
}
