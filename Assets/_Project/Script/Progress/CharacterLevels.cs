using System;
using System.Collections.Generic;
using Core.Save;
using UnityEngine;
using VContainer;

// Level and experience, kept PER CHARACTER — each main character carries its own, and a level is worth one
// upgrade point on that character's tree.
//
// This contradicts Docs/DESIGN.md, which still says characters "share everything that progresses — one
// wallet, one level, one gear inventory". The level half of that sentence is no longer true and the doc has
// not been rewritten yet.
//
// EXPERIENCE IS STORED AS PROGRESS INTO THE CURRENT LEVEL, NOT AS A RUNNING TOTAL, and that is the whole
// reason the save carries two numbers instead of one. A total would have to be re-divided by the curve every
// time it loads, so the day the curve is retuned every existing player silently jumps or drops a few levels.
// Storing (level, exp-into-level) means a retune changes what the NEXT level costs and nothing else: the
// player stays exactly where they were standing.
//
// THE CURVE IS HARD-CODED ON PURPOSE, for now. It wants to be a config eventually; putting it in one static
// method until then means the day it moves there is one call site, not a hunt.
public class CharacterLevels : ISavable
{
    public const int StartLevel = 1;

    // The ceiling, and it is a design number rather than a safety rail: one point per level means a capped
    // level is a capped POINT POOL, which is what turns a node's cost from "when do I buy this" into "do I
    // buy this instead of that". A tree worth more than MaxLevel points is where builds start.
    public const int MaxLevel = 100;

    const float BaseExp = 100f;    // to get from level 1 to level 2
    const float Growth = 1.07f;    // each level costs this much more than the one below it

    // GROWTH IS CHOSEN AGAINST THE CEILING, not by taste. Across 99 levels 1.07 doubles the cost about every
    // ten of them — ten doublings in all — so the last level costs roughly 800x the first (75,850 against
    // 100) and the whole climb is about 1.16 million. A steeper curve stops being readable long before the
    // top: 1.18, which this used to be, ends at 1.1 BILLION for the last level and overflows an int at 104.
    public static int ExpToLevelUp(int level)
        => level >= MaxLevel
            ? 0   // nothing left to buy — see AddExp, where a zero is what ends the climb
            : Mathf.RoundToInt(BaseExp * Mathf.Pow(Growth, Mathf.Max(0, level - StartLevel)));

    readonly Dictionary<string, int> _level = new Dictionary<string, int>();
    readonly Dictionary<string, int> _exp = new Dictionary<string, int>();
    readonly SaveService _save;

    public string SaveKey => "levels";

    // Fires for the character that moved, so a HUD bound to one body ignores the rest.
    public event Action<string> Changed;

    [Inject]
    public CharacterLevels(SaveService save)
    {
        _save = save;
        _save.Register(this);   // loads _level / _exp
    }

    // A character nobody has played yet is level 1 with nothing banked, and is never written until it moves.
    public int Level(string characterId)
        => !string.IsNullOrEmpty(characterId) && _level.TryGetValue(characterId, out int n) ? n : StartLevel;

    public int Exp(string characterId)
        => !string.IsNullOrEmpty(characterId) && _exp.TryGetValue(characterId, out int n) ? n : 0;

    public int ExpToNext(string characterId) => ExpToLevelUp(Level(characterId));

    public bool IsMaxLevel(string characterId) => Level(characterId) >= MaxLevel;

    // Full at the ceiling rather than empty. A bar that resets to nothing on the last level reads as a bar
    // that broke, and there is no next level for it to be part of the way towards.
    public float Fraction(string characterId)
    {
        int need = ExpToNext(characterId);
        return need > 0 ? Mathf.Clamp01(Exp(characterId) / (float)need) : 1f;
    }

    // Spends the experience level by level rather than dividing a total, so the leftover is always progress
    // into the level actually reached — a `while` because one drop can carry a character up several levels.
    //
    // THE CEILING NEEDS NO CHECK OF ITS OWN: ExpToLevelUp returns 0 at MaxLevel, and a price of zero is what
    // stops the loop. What is left over stays BANKED in the last level rather than being thrown away — the
    // day the ceiling is raised, a character that has been sitting at it for hours does not find those hours
    // were worth nothing. Nothing reads that overflow today, and nothing has to.
    public void AddExp(string characterId, int amount)
    {
        if (string.IsNullOrEmpty(characterId) || amount <= 0) return;

        int level = Level(characterId);
        int exp = Exp(characterId) + amount;

        int need = ExpToLevelUp(level);
        while (need > 0 && exp >= need)
        {
            exp -= need;
            level++;
            need = ExpToLevelUp(level);
        }

        Write(characterId, level, exp);
    }

    // Cheat / debug path. Sets the level outright and drops the part-level progress, because "level 7" with
    // three quarters of level 4 still banked is a state nothing else in the game can produce.
    public void SetLevel(string characterId, int level)
    {
        if (string.IsNullOrEmpty(characterId)) return;
        Write(characterId, Mathf.Clamp(level, StartLevel, MaxLevel), 0);
    }

    // NOT WRITTEN TO DISK HERE, and that is the one thing to know before wiring anything else into AddExp.
    // Experience moves on every kill, and a save is a file write — one per corpse is I/O nobody asked for,
    // and worst on the platform least able to afford it. Docs/DESIGN.md already names the only two things
    // that write: arriving home, and dying.
    //
    // Until those exist the numbers still survive a normal quit, because SaveService.SaveAll runs when the
    // scope is disposed. What a crash mid-run costs is the run — which is exactly what leaving home was
    // always supposed to cost.
    void Write(string characterId, int level, int exp)
    {
        _level[characterId] = level;
        _exp[characterId] = exp;
        Changed?.Invoke(characterId);
    }

    // Two flat dictionaries rather than one of a custom type: the save round-trips through Newtonsoft with
    // TypeNameHandling.Auto, and plain string->int is the shape already proven by PayGateSystem.
    public void Save(SaveBag bag)
    {
        bag.Set("Level", new Dictionary<string, int>(_level));
        bag.Set("Exp", new Dictionary<string, int>(_exp));
    }

    public void Load(SaveBag bag)
    {
        _level.Clear();
        _exp.Clear();

        // Clamped to the ceiling on the way in as well as on the way out: a save written before the cap
        // existed, or one somebody edited, must not walk back in above it.
        foreach (var kv in bag.Get("Level", new Dictionary<string, int>()))
            if (!string.IsNullOrEmpty(kv.Key)) _level[kv.Key] = Mathf.Clamp(kv.Value, StartLevel, MaxLevel);

        foreach (var kv in bag.Get("Exp", new Dictionary<string, int>()))
            if (!string.IsNullOrEmpty(kv.Key)) _exp[kv.Key] = Mathf.Max(0, kv.Value);
    }
}
