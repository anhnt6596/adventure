using System.Collections.Generic;
using Core;
using UnityEngine;

// All interact zones, in a spatial hash. Zones are near-static, so the hash rebuilds only when the
// set changes, not per frame. Query returns the zones actually containing a point.
public class InteractField
{
    const float CellSize = 8f;   // must exceed the largest zone; bump if zones get bigger

    readonly SpatialHash<InteractZone> _hash = new SpatialHash<InteractZone>(z => z.Center, CellSize);
    readonly List<InteractZone> _all = new List<InteractZone>();
    readonly List<InteractZone> _candidates = new List<InteractZone>();
    float _maxRadius = 1f;
    bool _dirty;

    public void Register(InteractZone z)
    {
        if (_all.Contains(z)) return;
        _all.Add(z);
        _hash.Add(z);
        _dirty = true;
    }

    public void Unregister(InteractZone z)
    {
        if (!_all.Remove(z)) return;
        _hash.Remove(z);
        _dirty = true;
    }

    public void QueryAt(Vector3 point, List<InteractZone> results)
    {
        if (_dirty)
        {
            _maxRadius = 1f;
            for (int i = 0; i < _all.Count; i++)
                _maxRadius = Mathf.Max(_maxRadius, _all[i].BoundingRadius);
            _hash.Rebuild();
            _dirty = false;
        }

        _hash.Query(point, _maxRadius, _candidates);
        results.Clear();
        for (int i = 0; i < _candidates.Count; i++)
            if (_candidates[i] != null && _candidates[i].Contains(point))
                results.Add(_candidates[i]);
    }
}
