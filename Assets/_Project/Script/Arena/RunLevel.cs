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
// THE SAME SHAPE AS CharacterLevels, WITH ITS OWN NUMBERS — one base, one growth, one static method. Not the
// arena's: a level cost belongs to the PLAYER, and an AnimationCurve per arena would have been a copy per
// place to drift out of step. An arena differs where it should, in what its monsters are worth and how
// thickly they come, so a harder place levels you faster by feeding you more.
//
// AND NOT CharacterLevels' OWN NUMBERS EITHER, because the two climbs are opposite problems:
//
//     character   99 levels across the whole game, fed by firsts that cannot be farmed
//     a run       twenty-odd levels in twenty minutes, fed by a horde
//
// A STRAIGHT LINE, NOT A COMPOUNDING ONE, and that is the genre's shape rather than a simplification. Each
// level costs a FIXED amount more than the one below it, so the total is quadratic — which is what keeps
// levels arriving right to the end of a run, thinning out instead of stopping.
//
// A compounding curve was tried here first and it is wrong for a run. Cost doubles every few levels while
// income does not: ArenaConfig.maxAlive caps how many can be alive at once, so the horde plateaus and the
// climb simply dies somewhere around level fifteen, with the last third of the run paying nothing at all.
// Over ninety-nine levels compounding is right — that is CharacterLevels' problem and it keeps 1.07. Over
// twenty it is a wall.
public class RunLevel
{
    const int BaseExp = 200;   // level 1 to 2. What that costs in KILLS is the monsters' business, not this
                               // number's — see EnemyConfig.exp.

    // How much MORE each level costs than the one below it, and it is what a run's whole climb is set by.
    //
    // SIZED AGAINST THE BODY COUNT, not by taste. A run kills THOUSANDS — that is the genre — and with a
    // straight line the total to level n is about Step x n^2 / 2, so
    //
    //     Step  ~  2 x (experience a whole run pays) / (levels a run should reach)^2
    //
    // Twenty-odd levels against a few thousand kills lands here. Get this wrong by an order of magnitude and
    // the mistake does not look like a tuning problem: at 40 the player is level forty-five by the end and the
    // cards have stopped meaning anything.
    const int Step = 200;

    public int Level { get; private set; } = 1;
    public int Exp { get; private set; }

    // Fires once per level gained — several times in a row when one kill carries the player through more than
    // one. The draft listens and queues a choice for each, so a big pick-up is several cards rather than one.
    public event Action LeveledUp;

    // Fires whenever the numbers move at all, level or experience. Separate from LeveledUp because a bar
    // filling and a card being offered are different events, and a HUD that listened to the second would sit
    // still for a whole level and then jump.
    public event Action Changed;

    public int ExpToNext => BaseExp + Step * Mathf.Max(0, Level - 1);

    public float Fraction => Mathf.Clamp01((float)Exp / ExpToNext);

    public void Award(int amount)
    {
        if (amount <= 0) return;
        Exp += amount;

        // A while, not an if: one fat kill late in a run can be worth several levels, and paying them one at a
        // time — carrying the remainder — is what makes the bar land where it should afterwards. The cost
        // climbs with every step, so the loop always ends.
        while (Exp >= ExpToNext)
        {
            Exp -= ExpToNext;
            Level++;
            LeveledUp?.Invoke();
        }

        Changed?.Invoke();
    }
}
