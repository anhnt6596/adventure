using System;

// The read side of a stat: what it is worth right now, and a way to be told when that changes.
//
// SPLIT FROM Stat SO THAT HOLDING ONE IS NOT PERMISSION TO BUFF IT. Everything that reads a stat — movement,
// an attack working out its damage, a bar on the HUD — takes this. Only whoever owns the character's stats
// holds the Stat itself and can put modifiers on or take them off. Without the split, any component that
// needed to know how fast it moves could also decide how fast it moves.
//
// Changed matters more than it looks: a stat can move without anything else happening — buying an upgrade
// raises max HP while the player stands still — so anyone showing a stat has to be told rather than notice.
public interface IStat
{
    float Value { get; }
    event Action Changed;
}
