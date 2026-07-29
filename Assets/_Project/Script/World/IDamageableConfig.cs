// The stats a Damageable needs off its kind: how much HP, which team it's on. A config SO implements this so
// Damageable never depends on the concrete asset type. Hit radius is deliberately NOT here — it belongs to the
// body, so it is a field on the Damageable component itself, authored against the art it has to line up with.
public interface IDamageableConfig
{
    float MaxHp { get; }
    int Team { get; }
}
