// One tile per cell, picked from which sides transition to another terrain.
// Weights 1/2/4/8 make every combination a distinct total: 0 = plain, 15 = lone cell.
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
