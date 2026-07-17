public class TerrainMap
{
    public int Width { get; }
    public int Height { get; }

    readonly byte[] _cells;

    public TerrainMap(int width, int height)
    {
        Width = width;
        Height = height;
        _cells = new byte[width * height];
    }

    public TerrainMap(int width, int height, byte[] cells)
    {
        Width = width;
        Height = height;
        _cells = cells;
    }

    public byte[] Cells => _cells;

    public bool InBounds(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;

    // Outside the map reads as terrain 0, so the base layer closes the map edge for free.
    public byte Get(int x, int y) => InBounds(x, y) ? _cells[y * Width + x] : (byte)0;

    public void Set(int x, int y, byte id)
    {
        if (InBounds(x, y)) _cells[y * Width + x] = id;
    }

    public TerrainMap Subdivide(int factor)
    {
        var fine = new TerrainMap(Width * factor, Height * factor);
        for (int y = 0; y < fine.Height; y++)
            for (int x = 0; x < fine.Width; x++)
                fine.Set(x, y, Get(x / factor, y / factor));
        return fine;
    }
}
