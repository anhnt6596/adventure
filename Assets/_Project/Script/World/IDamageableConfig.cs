// The stats a Damageable needs: how much HP, how big its hit circle, which team it's on. A config SO
// implements this so Damageable never depends on the concrete asset type.
public interface IDamageableConfig
{
    float MaxHp { get; }
    float HitRadius { get; }
    int Team { get; }
}
