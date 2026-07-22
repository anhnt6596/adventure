using System.Collections.Generic;
using UnityEngine;

// One-sided collision wall in the grid's local XZ plane (metres). The normal points to the walkable
// side; the terrain id lets a body's pass mask ignore walls it may cross (a swimmer over water).
[System.Serializable]
public struct WallSeg
{
    public Vector2 a;
    public Vector2 b;
    public Vector2 normal;
    public byte terrain;
}

// Bakes the walkable boundary from the terrain. An unwalkable tile shrinks by d=1/8 on each edge that
// borders walkable ground; at a convex corner the corner is chamfered off (a 45deg cut c=3/8 from it),
// at a concave corner it bulges out by the same. Each edge end is trimmed by c unless the boundary runs
// straight through it; each diagonal is emitted once by the cell that owns the corner.
public static class WalkBake
{
    const float D = 1f / 8f;   // straight inset
    const float V = 2f / 8f;   // convex chamfer trim: the 45deg cut lands 3/8 (= V + D) from the corner

    public static WallSeg[] Bake(TerrainMap map, bool[] walkable, float cellSize)
    {
        var walls = new List<WallSeg>();
        if (map == null || walkable == null) return walls.ToArray();

        bool Blk(int x, int y) => map.InBounds(x, y) && !walkable[map.Get(x, y)];

        // Trim for an edge end: V if the corner is convex (side open, chamfer eats it), else 0 - a
        // straight run continues, and a concave corner is closed by a small bulge at the grid corner.
        static float Trim(bool side) => side ? 0f : V;

        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                if (!Blk(x, y)) continue;
                byte id = map.Get(x, y);

                bool bN = Blk(x, y + 1), bS = Blk(x, y - 1), bE = Blk(x + 1, y), bW = Blk(x - 1, y);
                bool bNE = Blk(x + 1, y + 1), bNW = Blk(x - 1, y + 1), bSE = Blk(x + 1, y - 1), bSW = Blk(x - 1, y - 1);

                float x0 = x, x1 = x + 1, y0 = y, y1 = y + 1;

                // Straight inset edges, each end trimmed only at a convex corner.
                if (!bN)
                    Add(walls, cellSize, x0 + Trim(bW), y1 - D, x1 - Trim(bE), y1 - D, 0, 1, id);
                if (!bS)
                    Add(walls, cellSize, x0 + Trim(bW), y0 + D, x1 - Trim(bE), y0 + D, 0, -1, id);
                if (!bE)
                    Add(walls, cellSize, x1 - D, y0 + Trim(bS), x1 - D, y1 - Trim(bN), 1, 0, id);
                if (!bW)
                    Add(walls, cellSize, x0 + D, y0 + Trim(bS), x0 + D, y1 - Trim(bN), -1, 0, id);

                // Convex corner (this cell alone sticks out): chamfer it off on the 45deg line 3/8 out.
                if (!bN && !bE && !bNE) Add(walls, cellSize, x1 - V, y1 - D, x1 - D, y1 - V, 1, 1, id);
                if (!bN && !bW && !bNW) Add(walls, cellSize, x0 + V, y1 - D, x0 + D, y1 - V, -1, 1, id);
                if (!bS && !bE && !bSE) Add(walls, cellSize, x1 - V, y0 + D, x1 - D, y0 + V, 1, -1, id);
                if (!bS && !bW && !bSW) Add(walls, cellSize, x0 + V, y0 + D, x0 + D, y0 + V, -1, -1, id);

                // Concave corner (this cell wraps an open diagonal): a small 1/8 bulge at the grid corner
                // instead of poking the full notch into the blocking.
                if (bN && bE && !bNE) Add(walls, cellSize, x1, y1 - D, x1 - D, y1, 1, 1, id);
                if (bN && bW && !bNW) Add(walls, cellSize, x0, y1 - D, x0 + D, y1, -1, 1, id);
                if (bS && bE && !bSE) Add(walls, cellSize, x1, y0 + D, x1 - D, y0, 1, -1, id);
                if (bS && bW && !bSW) Add(walls, cellSize, x0, y0 + D, x0 + D, y0, -1, -1, id);
            }

        return walls.ToArray();
    }

    static void Add(List<WallSeg> walls, float s, float ax, float ay, float bx, float by, float nx, float ny, byte id)
        => walls.Add(new WallSeg
        {
            a = new Vector2(ax * s, ay * s),
            b = new Vector2(bx * s, by * s),
            normal = new Vector2(nx, ny).normalized,
            terrain = id,
        });
}
