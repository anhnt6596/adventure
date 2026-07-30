using System.Collections.Generic;
using UnityEngine;

// Something that makes cells walkable which the terrain says are not: a bridge deck over water, a plank over a
// pit. It does NOT touch the terrain. The cell underneath stays water, so the water surface still draws under
// the deck and the shore distance the water shader is built on never moves - only walkability is overridden.
//
// Registration is self-service through TerrainGrid.AddDeck/RemoveDeck in OnEnable/OnDisable, the same way a
// CollisionBody joins the collision world. A deck riding a map prefab and a deck spawned mid-game therefore take
// the same path, and nothing has to keep a list of them.
public interface IDeck
{
    // A drawbridge raises and lowers without being created and destroyed: false takes the footprint away and
    // leaves the art alone.
    bool DeckActive { get; }

    // Appends the cells this deck covers. Called on the grid's terms, so it may be asked again at any time -
    // it must not assume it is only called once.
    void CollectDeckCells(TerrainGrid grid, List<Vector2Int> into);
}
