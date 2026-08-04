using System;
using System.Collections.Generic;

// One number a character has, plus everything currently pushing it around.
//
//     final = (base * Mul + Add) * FinalMul
//
// with Add summed (0 when empty) and both multiplies producted (1 when empty), so a stat with nothing on it
// is exactly its base. See StatModKind for why there are two multiplies.
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

    void Invalidate()
    {
        _dirty = true;
        Changed?.Invoke();
    }

    float Recalculate()
    {
        float add = 0f;
        float mul = 1f;
        float finalMul = 1f;

        foreach (var m in _mods)
        {
            switch (m.Kind)
            {
                case StatModKind.Add: add += m.Value; break;
                case StatModKind.Mul: mul *= m.Value; break;
                case StatModKind.FinalMul: finalMul *= m.Value; break;
            }
        }

        return (_base * mul + add) * finalMul;
    }

    // Spelled out rather than pulling UnityEngine in for one call: this file is plain C# and there is no
    // reason for a number holder to depend on the engine.
    static bool NearlyEqual(float a, float b) => Math.Abs(a - b) < 1e-6f;
}
