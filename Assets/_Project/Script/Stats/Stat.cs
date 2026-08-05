using System;
using System.Collections.Generic;

// One number a character has, plus everything currently pushing it around.
//
//     final = (base * (1 + Mul) + Add) * (1 + FinalMul)
//
// with EVERY KIND SUMMED, the multiplies included, because a multiplier is carried as what it ADDS: 0.1 is
// +10%. So two +10% come to +20% and not +21%, and the neutral value of all three is 0 — a stat with nothing
// on it is exactly its base. See StatModKind for why there are two multiplies, and StatModifier for the
// arithmetic and what it costs.
//
// RECALCULATED LAZILY, ANNOUNCED EAGERLY. The value is only worked out when somebody asks for it and the
// modifiers have moved since — a stat read every frame by movement costs one float read. But Changed fires
// the moment a modifier lands, because the things that need to react (a bar redrawing, HP catching up to a
// new maximum) have no other way to find out and cannot poll something they only read once.
public class Stat : IStat
{
    readonly List<StatModifier> _mods = new List<StatModifier>();
    float _base;
    float _value;
    bool _dirty = true;

    int _batch;         // > 0 while several moves are being made that the outside should read as one
    bool _batched;      // something moved while it was held
    float _batchValue;  // what it was worth when the batch opened

    public event Action Changed;

    public Stat(float baseValue) => _base = baseValue;

    // The character's own number, before anything modifies it. Settable because a base can legitimately move
    // — a permanent upgrade that raises what the character IS rather than buffing it — and that has to
    // announce itself like any other change.
    public float BaseValue
    {
        get => _base;
        set
        {
            if (NearlyEqual(_base, value)) return;
            _base = value;
            Invalidate();
        }
    }

    public float Value
    {
        get
        {
            if (_dirty) { _value = Recalculate(); _dirty = false; }
            return _value;
        }
    }

    public void Add(StatModifier modifier)
    {
        if (modifier == null) return;
        _mods.Add(modifier);
        Invalidate();
    }

    public void RemoveBySource(object source)
    {
        if (source == null) return;
        if (_mods.RemoveAll(m => ReferenceEquals(m.Source, source)) > 0) Invalidate();
    }

    // Hold the announcements while a caller makes several moves that are really one — the upgrade tree taking
    // everything off and putting it back is the case this exists for.
    //
    // WITHOUT IT A REBUILD IS A COLLAPSE AND A CLIMB, and that is not a private detail: a listener reacting to
    // the way a stat MOVES sees the ceiling fall to the base and rise again, and acts on both. Damageable is
    // exactly that listener — it clamps HP on the way down and hands the difference over on the way up — so a
    // rebuild used to knock the player down to their unbuffed maximum, or hand them free health for buying
    // something that has nothing to do with health. One announcement, and only if the number really ended up
    // somewhere else, is what makes a rebuild mean "this is the new value" instead.
    public void BeginBatch()
    {
        if (_batch++ == 0)
        {
            _batchValue = Value;
            _batched = false;
        }
    }

    public void EndBatch()
    {
        if (_batch == 0 || --_batch > 0) return;
        if (!_batched) return;

        _batched = false;
        if (NearlyEqual(_batchValue, Value)) return;   // taken off and put back the same: nobody has to know
        Changed?.Invoke();
    }

    void Invalidate()
    {
        _dirty = true;
        if (_batch > 0) { _batched = true; return; }
        Changed?.Invoke();
    }

    float Recalculate()
    {
        float add = 0f;
        float mul = 0f;         // the shares, summed — 0 leaves the base as it is
        float finalMul = 0f;

        foreach (var m in _mods)
        {
            switch (m.Kind)
            {
                case StatModKind.Add: add += m.Value; break;
                case StatModKind.Mul: mul += m.Value; break;
                case StatModKind.FinalMul: finalMul += m.Value; break;
            }
        }

        return (_base * (1f + mul) + add) * (1f + finalMul);
    }

    // Spelled out rather than pulling UnityEngine in for one call: this file is plain C# and there is no
    // reason for a number holder to depend on the engine.
    static bool NearlyEqual(float a, float b) => Math.Abs(a - b) < 1e-6f;
}
