using UnityEngine;

// Grid math helpers for the sealed GridBuild module.
// Inlined from the jam's MathUtils so this module has no game dependencies.
public static class GridMath
{
    // Centering offset for a footprint of `size` cells (so it centers on the anchor).
    public static Vector3 GetOffsetXZ(GridSize size, float cellSize)
    {
        return new Vector3((size.w - 1) / 2f * cellSize, 0f, (size.h - 1) / 2f * cellSize);
    }
}
