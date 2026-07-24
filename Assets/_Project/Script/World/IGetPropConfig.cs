// Wall over ConfigRegistry for prop/destructible configs: fetch a PropConfig (HP/hit-radius/team + drops)
// by id. A Prop asks this by its own Id — it never touches the registry directly.
public interface IGetPropConfig
{
    IDamageableConfig Get(string id);
}
