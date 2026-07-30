using UnityEngine;

// One-sided collision face in the grid's local XZ plane (metres). The normal points to the walkable side; the
// terrain id lets a body's pass mask ignore faces it may cross (a swimmer over water).
//
// These are NOT stored anywhere. TerrainGrid.CellFaces generates the handful belonging to one cell on demand, and
// CollisionWorld asks only for the cells a body can actually reach - so nothing has to be baked, nothing can go
// stale, and a bridge deck or a painted tile takes effect on the very next tick.
[System.Serializable]
public struct WallSeg
{
    public Vector2 a;
    public Vector2 b;
    public Vector2 normal;
    public byte terrain;

    // The face geometry, shared by the generator and anything that wants to reason about it.
    // An unwalkable tile shrinks by D on each edge bordering walkable ground; at a convex corner the corner is
    // chamfered off on the 45 degree line, landing V + D from it; at a concave corner it bulges out by D instead
    // of poking the full notch into the blocking.
    public const float Inset = 1f / 8f;
    public const float Chamfer = 2f / 8f;

    public const int MaxPerCell = 12;   // 4 straight edges + 4 convex chamfers + 4 concave bulges
}
