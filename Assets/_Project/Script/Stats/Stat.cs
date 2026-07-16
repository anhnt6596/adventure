using System.Collections.Generic;

public class Stat
{
    public float BaseValue;

    readonly List<StatModifier> _mods = new List<StatModifier>();
    float _value;
    bool _dirty = true;

    public Stat(float baseValue) => BaseValue = baseValue;

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
        _mods.Add(modifier);
        _dirty = true;
    }

    public void RemoveBySource(object source)
    {
        if (_mods.RemoveAll(m => m.Source == source) > 0) _dirty = true;
    }

    float Recalculate()
    {
        float flat = BaseValue;
        float percentAdd = 0f;

        foreach (var m in _mods)
        {
            if (m.Type == StatModType.Flat) flat += m.Value;
            else if (m.Type == StatModType.PercentAdd) percentAdd += m.Value;
        }

        float result = flat * (1f + percentAdd);

        foreach (var m in _mods)
            if (m.Type == StatModType.PercentMult) result *= 1f + m.Value;

        return result;
    }
}
