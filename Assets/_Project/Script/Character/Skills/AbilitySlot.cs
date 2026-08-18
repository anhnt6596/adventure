// WHICH BUTTON RUNS AN ABILITY. Not "which of the two skills this is": every press a character answers is one
// of these components now — the attack included, the dash included — so the binding has to be able to name all
// four buttons rather than the two that used to be special.
//
// NONE IS A REAL VALUE, and the one that makes a combo possible. A step of a string is an ability in every way
// except that no button reaches it: the thing that drives it decides when it happens. Without None, every piece
// of a five-hit string would have to claim a button it must never answer.
//
// It is also the DEFAULT, so a component dropped on a prefab and forgotten is silent. Defaulting to Attack
// would let a half-wired piece quietly take the attack button off the ability that was supposed to have it.
public enum AbilitySlot
{
    None = 0,
    Attack = 1,
    Dash = 2,
    Skill1 = 3,
    Skill2 = 4,
}
