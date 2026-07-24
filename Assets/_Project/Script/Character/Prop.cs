using VContainer;

// A static, breakable world unit — tree, rock, chest. No movement (a plain Unit runs no loop). Its
// HP/hit-radius/team and death drops come from a PropConfig resolved by id from the registry
// (IGetPropConfig), so nothing is dragged onto the prefab. Placed on a map, it's injected when the map
// instantiates — the same DI pass that wires any other map child.
public class Prop : Unit
{
    IDamageableConfig _config;

    public override int Team => _config?.Team ?? 2;
    public override IDamageableConfig DamageableConfig => _config;

    [Inject]
    public void Construct(IGetPropConfig configs) => _config = configs.Get(Id);
}
