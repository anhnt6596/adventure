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

    const float BaseExp = 100f;    // to get from level 1 to level 2
    const float Growth = 1.18f;    // each level costs this much more than the one below it

    // What it costs to go from `level` to `level + 1`.
    public static int ExpToLevelUp(int level)
        => Mathf.RoundToInt(BaseExp * Mathf.Pow(Growth, Mathf.Max(0, level - StartLevel)));

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

    public float Fraction(string characterId)
    {
        int need = ExpToNext(characterId);
        return need > 0 ? Mathf.Clamp01(Exp(characterId) / (float)need) : 0f;
    }

    // Spends the experience level by level rather than dividing a total, so the leftover is always progress
    // into the level actually reached — a `while` because one drop can carry a character up several levels.
    public void AddExp(string characterId, int amount)
    {
        if (string.IsNullOrEmpty(characterId) || amount <= 0) return;

        int level = Level(characterId);
        int exp = Exp(characterId) + amount;

        int need = ExpToLevelUp(level);
        while (exp >= need && need > 0)
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
        Write(characterId, Mathf.Max(StartLevel, level), 0);
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

        foreach (var kv in bag.Get("Level", new Dictionary<string, int>()))
            if (!string.IsNullOrEmpty(kv.Key)) _level[kv.Key] = Mathf.Max(StartLevel, kv.Value);

        foreach (var kv in bag.Get("Exp", new Dictionary<string, int>()))
            if (!string.IsNullOrEmpty(kv.Key)) _exp[kv.Key] = Mathf.Max(0, kv.Value);
    }
}
