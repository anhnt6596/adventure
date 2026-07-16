using UnityEngine;

public abstract class Config : ScriptableObject
{
    [SerializeField] string id;
    public string Id => string.IsNullOrEmpty(id) ? name : id;
}
