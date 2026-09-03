using System;
using UnityEngine;

// How a run card moves one of the character's numbers.
//
// COMPOUND IS WHY THIS IS NOT StatBuff. The world's modifier system carries multipliers as SHARES THAT ADD UP
// — two +25% come to +50%, on purpose, so a deep tree prices in a straight line. A run is the opposite shape:
// twenty minutes, a dozen cards, and the fantasy is a build running away with itself. Attack taken four times
// should be 1.25^4 = 2.44x, not 2x, and there is no setting of the outside system that says that.
//
// Rather than teach that system a second kind of arithmetic — which would be a new stacking rule every stat
// in the game has to be checked against — the run keeps its own little layer with the formula written out
// (see RunStats). It is allowed to be simple: nothing here is saved, nothing balances against gear, and the
// whole thing is thrown away at the end of the run.
public enum RunBuffKind
{
    // Flat, summed. +25 crit points taken twice is +50.
    Add,

    // A share applied MULTIPLICATIVELY, once per stack: 0.25 taken three times is x1.25 x1.25 x1.25.
    Compound,
}

[Serializable]
public struct RunBuff
{
    [Tooltip("Which stat, by StatId — AttackPower, AttackSpeed, MoveSpeed, CritPoints, CritDamage...")]
    public string stat;

    [Tooltip("Add is flat. Compound is a share multiplied in once per stack — 0.25 is x1.25 each time, so " +
             "four of them is 2.44x rather than 2x.")]
    public RunBuffKind kind;

    public float amount;

    // NO NUMBER, ON PURPOSE. A card says which way it pushes and nothing else — "+ attack", not "x1.25
    // attack". The player is meant to feel a build getting away from them rather than do arithmetic in the
    // three seconds a horde gives them, and a card that hides its size stays worth taking on the tenth run.
    //
    // The direction is still honest: a card that takes something away says so.
    public string Describe() => $"{(amount >= 0f ? "+" : "-")} {StatId.Display(stat)}";
}
