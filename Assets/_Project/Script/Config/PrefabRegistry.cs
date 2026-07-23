using System.Collections.Generic;
using UnityEngine;

// The prefab twin of ConfigRegistry: one asset holding every id-carrying prefab, keyed by Identifiable.Id.
// Simpler than ConfigRegistry — a prefab is looked up by id alone (no type key), since the id IS the thing
// that says "spawn this". Stores Identifiable (a MonoBehaviour, so Unity serialises the list of prefab-root
// component refs); .gameObject on each is the prefab to Instantiate.
[CreateAssetMenu(menuName = "Config/Prefab Registry")]
public class PrefabRegistry : ScriptableObject
{
    [SerializeField] List<Identifiable> prefabs = new List<Identifiable>();

    Dictionary<string, Identifiable> _lookup;

    public Identifiable Get(string id)
    {
        Build();
        return _lookup.TryGetValue(id, out var p) ? p : null;
    }

    void Build()
    {
        if (_lookup != null) return;
        _lookup = new Dictionary<string, Identifiable>();
        foreach (var p in prefabs)
        {
            if (p == null) continue;
            _lookup[p.Id] = p;
        }
    }

#if UNITY_EDITOR
    public void SetAll(List<Identifiable> list)
    {
        prefabs = list;
        _lookup = null;
    }
#endif
}
