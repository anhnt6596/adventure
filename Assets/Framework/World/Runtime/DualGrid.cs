// Dual grid: the display grid sits half a cell off the logic grid, so a display tile straddles the
// corner where four logic cells meet. Four corners = 16 tiles per terrain instead of the 47 a
// same-grid blob tileset needs, with the same organic silhouette.
//
// Display tile (i,j) is centred on the corner between cells (i-1,j-1)..(i,j) and spans
// (i-0.5 .. i+0.5) in cell units. i runs 0..Width inclusive, so a WxH map needs (W+1)x(H+1) tiles.
public static class DualGrid
{
    public const int MaskCount = 16;

    public const int BitSouthWest = 1;
    public const int BitSouthEast = 2;
    public const int BitNorthWest = 4;
    public const int BitNorthEast = 8;

    public static int CornerMask(TerrainMap map, int i, int j, bool[] inLayer)
    {
        int mask = 0;
        if (inLayer[map.Get(i - 1, j - 1)]) mask |= BitSouthWest;
        if (inLayer[map.Get(i, j - 1)]) mask |= BitSouthEast;
        if (inLayer[map.Get(i - 1, j)]) mask |= BitNorthWest;
        if (inLayer[map.Get(i, j)]) mask |= BitNorthEast;
        return mask;
    }
}
