using System;

[Serializable]
public struct GridPos
{
    public GridPos(int x, int y) { this.x = x; this.y = y; }
    public int x;
    public int y;
}

[Serializable]
public struct GridSize
{
    public GridSize(int w, int h) { this.w = w; this.h = h; }
    public int w;
    public int h;
}

[Serializable]
public struct GridOccupy
{
    public GridOccupy(GridPos pos, GridSize size) { this.pos = pos; this.size = size; }
    public GridOccupy(int x, int y, int w, int h) { pos = new GridPos(x, y); size = new GridSize(w, h); }
    public int x => pos.x;
    public int y => pos.y;
    public int w => size.w;
    public int h => size.h;
    public GridPos pos;
    public GridSize size;
}
