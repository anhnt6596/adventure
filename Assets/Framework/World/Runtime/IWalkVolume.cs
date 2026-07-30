using UnityEngine;

// One edge of a volume's outline, as the volume describes it: WORLD space, y ignored. `inward` points into the
// volume - the side you are allowed to be on.
public struct WorldEdge
{
    public Vector3 a, b;
    public Vector3 inward;
}

// Free-form walkable geometry laid OVER the terrain: a bridge deck, a plank, a platform. It is its own geometry,
// authored in world units and answered analytically - it is never rasterised into cells and the tilemap is never
// told it exists. TerrainQuery is the only thing that reads it, and only when answering a query.
//
// EVERYTHING HERE IS WORLD SPACE, on purpose. A volume knows its own transform and nothing else - not the grid,
// not the cell size, not which map it is on. TerrainQuery owns the conversion into whatever space it compares in,
// because that space is the tilemap's business and a bridge should not have heard of it.
//
// A volume says exactly two things about itself - where it COVERS ground, and where its OUTLINE is - and it says
// them about its whole self, with no notion of which edges matter. It does not decide where you can step on.
//
// That falls out of what a wall is. The walkable set is the UNION of the tilemap's and every volume's, so the
// wall is the boundary of that union, and the boundary of a union is:
//
//     d(A u B) = (dA \ interior(B)) u (dB \ interior(A))
//
// Read for a bridge: the tilemap's faces survive except where a deck covers them (that is what lets you walk out
// over the river), and a deck's outline survives except where the walkable set already covers it (that is what
// makes the ends open where they meet the shore). One rule, applied twice, symmetric.
//
// Nothing here declares a "side" or an "end". The ends of a bridge are open because they lie on walkable ground,
// not because anything said so - which is also why a deck crossing an island opens onto the island, and why two
// decks meeting end to end do not wall each other off. Those are not special cases; they are the same rule.
public interface IWalkVolume
{
    // A drawbridge raises, a platform despawns: false takes the whole volume out of every query, geometry and all.
    bool VolumeActive { get; }

    // Bumped when the shape or the transform moves. TerrainQuery watches it to know when the geometry it cached
    // from this volume went stale; it does not otherwise care what changed.
    int VolumeVersion { get; }

    // World-space XZ bounds. Only x and z are read.
    Bounds WorldBounds { get; }

    // Is this world point on the deck? Analytic and sub-cell - this is the whole reason the volume is not a set
    // of cells.
    bool Contains(Vector3 world);

    // The stretches of the segment a->b that lie on this deck, appended to `into` from `at` as (t0, t1) pairs in
    // [0,1], and returning the new count. EXACT, not sampled: this is what cuts the tilemap's walls away where a
    // deck crosses them, and sampling it would leave an invisible wall wherever a deck is thinner than the
    // sample spacing - a bug that appears once and cannot be found.
    int Overlap(Vector3 a, Vector3 b, float[] into, int at);

    // Upper bound on the values Overlap can write (two per interval).
    int MaxOverlapValues { get; }

    // The volume's COMPLETE outline, appended from `at`, returning the new count. Every edge, with no judgement
    // about which ones should end up being walls - TerrainQuery decides that by subtracting what is already
    // walkable, and it is the only thing that can, because only it knows what else is there.
    int Outline(WorldEdge[] into, int at);

    // Upper bound on what Outline can write, so a caller can size a buffer.
    int MaxOutlineEdges { get; }
}
