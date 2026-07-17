// Same-grid autotiling: one tile per cell, picked from which of its four neighbours are a different
// terrain - i.e. which sides need a transition drawn. This is the arrangement ready-made tilesets
// are drawn for.
//
// A bit is set when that side transitions, so mask 0 is the plain interior tile and mask 15 is a
// lone cell fringed all round. The 1/2/4/8 weights make every combination a distinct total.
public static class SameGrid
{
    public const int MaskCount = 16;

    public const int OpenNorth = 1;
    public const int OpenEast = 2;
    public const int OpenSouth = 4;
    public const int OpenWest = 8;

    public static int NeighbourMask(TerrainMap map, int x, int y, bool[] inLayer)
    {
        int mask = 0;
        if (!inLayer[map.Get(x, y + 1)]) mask |= OpenNorth;
        if (!inLayer[map.Get(x + 1, y)]) mask |= OpenEast;
        if (!inLayer[map.Get(x, y - 1)]) mask |= OpenSouth;
        if (!inLayer[map.Get(x - 1, y)]) mask |= OpenWest;
        return mask;
    }
}
