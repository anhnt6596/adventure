using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Config/Registry")]
public class ConfigRegistry : ScriptableObject
{
    [SerializeField] List<Config> configs = new List<Config>();

    Dictionary<Type, Dictionary<string, Config>> _lookup;

    public T Get<T>(string id) where T : Config
    {
        Build();
        return _lookup.TryGetValue(typeof(T), out var byId) && byId.TryGetValue(id, out var c)
            ? (T)c
            : null;
    }

    public IEnumerable<T> All<T>() where T : Config
    {
        foreach (var c in configs)
            if (c is T t) yield return t;
    }

    void Build()
    {
        if (_lookup != null) return;
        _lookup = new Dictionary<Type, Dictionary<string, Config>>();
        foreach (var c in configs)
        {
            if (c == null) continue;
            var t = c.GetType();
            if (!_lookup.TryGetValue(t, out var byId))
                _lookup[t] = byId = new Dictionary<string, Config>();
            byId[c.Id] = c;
        }
    }

#if UNITY_EDITOR
    public void SetAll(List<Config> list)
    {
        configs = list;
        _lookup = null;
    }
#endif
}
