using System;
using UnityEngine;

// The level the player climbs INSIDE one run. Born with the run, gone with it.
//
// NOT CharacterLevels, AND THE TWO MUST NEVER MEET. That one is the character's, banked in the save, and
// Docs/GATE_RUN.md turns on the two never mixing: what is earned in an arena buys power for that arena only,
// and the world's own level is fed by "firsts" that cannot be farmed. Sharing a class between them would be
// one edit away from a run's kills quietly levelling the save.
//
// LEVELLING IS THE ONLY THING IT DOES. It does not know what a level is worth — the draft does — so a run
// that offers no cards still levels, and a deck can be changed without touching this.
//
// EXP TO NEXT GROWS WITH THE LEVEL, and it is authored as a curve on the arena rather than a formula here: a
// short test arena and a long one want completely different climbs, and a formula would put that difference
// in code where nobody balancing can see it.
public class RunLevel
{
    readonly AnimationCurve _toNext;

    public int Level { get; private set; } = 1;
    public int Exp { get; private set; }

    // Fires once per level gained — several times in a row when one kill carries the player through more than
    // one. The draft listens and queues a choice for each, so a big pick-up is several cards rather than one.
    public event Action LeveledUp;

    public RunLevel(AnimationCurve expToNext) => _toNext = expToNext;

    // What the NEXT level costs. Floored at 1 so a curve authored to zero cannot spin the loop below forever.
    public int ExpToNext => Mathf.Max(1, Mathf.RoundToInt(_toNext?.Evaluate(Level) ?? 1f));

    public float Fraction => Mathf.Clamp01((float)Exp / ExpToNext);

    public void Award(int amount)
    {
        if (amount <= 0) return;
        Exp += amount;

        // A while, not an if: one fat kill late in a run can be worth several levels, and paying them one at a
        // time — carrying the remainder — is what makes the bar land where it should afterwards.
        while (Exp >= ExpToNext)
        {
            Exp -= ExpToNext;
            Level++;
            LeveledUp?.Invoke();
        }
    }
}
