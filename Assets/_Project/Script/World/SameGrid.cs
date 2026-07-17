// Same-grid autotiling: one tile per cell, chosen from which of its four neighbours belong to the
// same layer. This is the arrangement ready-made tilesets are drawn for.
public static class SameGrid
{
    public const int MaskCount = 16;

    public const int BitNorth = 1;
    public const int BitEast = 2;
    public const int BitSouth = 4;
    public const int BitWest = 8;

    // A bit is set when that neighbour is part of the layer, so mask 15 is a fully enclosed cell
    // and mask 0 is an isolated one.
    public static int NeighbourMask(TerrainMap map, int x, int y, bool[] inLayer)
    {
        int mask = 0;
        if (inLayer[map.Get(x, y + 1)]) mask |= BitNorth;
        if (inLayer[map.Get(x + 1, y)]) mask |= BitEast;
        if (inLayer[map.Get(x, y - 1)]) mask |= BitSouth;
        if (inLayer[map.Get(x - 1, y)]) mask |= BitWest;
        return mask;
    }

    public static string NameOf(int mask)
    {
        bool n = (mask & BitNorth) != 0, e = (mask & BitEast) != 0;
        bool s = (mask & BitSouth) != 0, w = (mask & BitWest) != 0;

        if (n && e && s && w) return "centre";
        if (!n && !e && !s && !w) return "single";
        if (n && s && !e && !w) return "vertical";
        if (e && w && !n && !s) return "horizontal";

        string open = "";
        if (!n) open += "N";
        if (!e) open += "E";
        if (!s) open += "S";
        if (!w) open += "W";
        return $"open {open}";
    }
}
