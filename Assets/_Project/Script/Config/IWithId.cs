// Anything that carries a stable string id: a Config (SO side) or an Identifiable (prefab side). Registries
// and the id-keyed lookups depend on this, not on the concrete asset type.
public interface IWithId
{
    string Id { get; }
}
