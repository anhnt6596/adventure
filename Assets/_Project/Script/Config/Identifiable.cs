using UnityEngine;

// The prefab-side counterpart to Config: a root component inherits this to carry its OWN id, so PrefabRegistry
// can key it and no separate id-component has to be fetched. Non-abstract on purpose (unlike Config) — a plain
// prop can add it directly, while an entity (Character, Enemy) inherits it.
public class Identifiable : MonoBehaviour, IWithId
{
    [SerializeField] string id;
    public string Id => string.IsNullOrEmpty(id) ? name : id;   // empty id => GameObject/prefab name
}
