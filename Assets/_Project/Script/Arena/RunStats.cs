using System.Collections.Generic;
using UnityEngine;

// The character's numbers as a RUN sees them: what they were on walking in, plus everything the cards have
// piled on since.
//
// IT CACHES THE ENTRY VALUE AND OWNS EVERYTHING ABOVE IT. The world's stat system — bases, gear, the upgrade
// tree — has already had its say by the time a run opens, and its answer is a single number per stat. That
// number is this layer's floor, read once and never re-read: nothing outside can move a stat mid-run (the
// tree is bought at home, gear is changed at home), so re-deriving it every card would be arithmetic looking
// for a change that cannot happen.
//
// AND IT MEANS THE TWO SYSTEMS NEVER ARGUE. A run stacking multiplicatively while the tree stacks additively
// is not a contradiction if they are separate layers — it is only a contradiction if they share a bucket.
//
// THE RESULT IS PUSHED BACK AS ONE MODIFIER PER STAT, tagged with the run. That is what makes leaving an
// arena free: RemoveBySource and the character is exactly what walked in. It is also why the delta is
// MEASURED rather than computed — the run takes its own modifier off, reads what the world says, and adds
// back the difference to where it wants to be. No assumption about the outside formula, which is a formula
// this layer has no business knowing.
//
// (One assumption survives: the delta rides in the Add bucket, so a FinalMul on the same stat would scale it.
// Nothing uses FinalMul today. If something ever does, this is the line that needs a divide.)
public class RunStats
{
    readonly MainCharStats _stats;
    readonly object _source;

    readonly Dictionary<string, float> _entry = new Dictionary<string, float>();   // value on walking in
    readonly Dictionary<string, float> _flat = new Dictionary<string, float>();    // summed Add
    readonly Dictionary<string, float> _factor = new Dictionary<string, float>();  // multiplied Compound

    public RunStats(MainCharStats stats, object source)
    {
        _stats = stats;
        _source = source;
    }

    public void Apply(RunBuff buff)
    {
        if (string.IsNullOrEmpty(buff.stat)) return;

        var stat = _stats?.Modifiable(buff.stat);
        if (stat == null)
        {
            Debug.LogWarning($"[{nameof(RunStats)}] a card names '{buff.stat}', which is not a stat on this " +
                             "character — it does nothing. Check the spelling against StatId.");
            return;
        }

        Remember(buff.stat, stat);

        if (buff.kind == RunBuffKind.Compound) _factor[buff.stat] *= 1f + buff.amount;
        else _flat[buff.stat] += buff.amount;

        Push(buff.stat, stat);
    }

    // Read once, the first time a card touches this stat — and with the run's own modifier off, so a second
    // card cannot cache a floor that already includes the first one's work and compound it twice.
    void Remember(string id, Stat stat)
    {
        if (_entry.ContainsKey(id)) return;

        stat.RemoveBySource(_source);
        _entry[id] = stat.Value;
        _flat[id] = 0f;
        _factor[id] = 1f;
    }

    // THE FORMULA, WRITTEN OUT: flat lands first, then everything multiplies. Add before multiply because a
    // card that gives +25 crit points and a card that gives x1.25 to them should not be worth different
    // amounts depending on which was drawn first — one order has to be chosen, and this is the one where the
    // multipliers are worth what the card says they are worth.
    void Push(string id, Stat stat)
    {
        float target = (_entry[id] + _flat[id]) * _factor[id];

        stat.RemoveBySource(_source);            // off first: what is left is the world's answer
        float world = stat.Value;
        stat.Add(new StatModifier(target - world, StatModKind.Add, _source));
    }

    // What the run has done to a stat, for a HUD that wants to show it. 1 means untouched.
    public float FactorOf(string id) => _factor.TryGetValue(id, out float f) ? f : 1f;

    // Everything this layer added, gone. The character is what walked in.
    public void Dispose()
    {
        if (_stats == null) return;

        foreach (var id in _entry.Keys)
            _stats.Modifiable(id)?.RemoveBySource(_source);

        _entry.Clear();
        _flat.Clear();
        _factor.Clear();
    }
}
