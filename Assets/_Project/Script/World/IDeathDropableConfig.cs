// What a thing provides when it dies. Separate from IDamageableConfig so the two concerns (taking
// damage / dropping loot) stay independent — a config SO can satisfy one or both.
public interface IDeathDropableConfig
{
    DeathDrop[] Drops { get; }
}
