using System.Collections.Generic;
using UnityEngine;

// Owned by whatever deals the damage. An arrow that keeps flying overlaps the same enemy for many
// frames; the source remembers who it has hit so each one is hit once.
//
// Not invulnerability frames on the target: those eat a second, legitimate hit landing in the same
// window, so two arrows fired together would do the damage of one.
public class HitRegister
{
    readonly Dictionary<IDamageable, float> _lastHit = new Dictionary<IDamageable, float>();

    // 0 or less means never again - a projectile. A positive value is a lingering source ticking at
    // that interval.
    readonly float _interval;

    public HitRegister(float interval = 0f) => _interval = interval;

    public bool TryHit(IDamageable target, float time)
    {
        if (_lastHit.TryGetValue(target, out float last))
        {
            if (_interval <= 0f) return false;
            if (time - last < _interval) return false;
        }

        _lastHit[target] = time;
        return true;
    }

    public void Clear() => _lastHit.Clear();
}
