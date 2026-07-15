using UnityEngine;

public interface IGrid
{
    int Width { get; }
    int Height { get; }
    float CellSize { get; }
    bool InBounds(int x, int y);
    bool IsEmpty(int x, int y);
    bool WorldToCell(Vector3 world, out int x, out int y);
    Vector3 CellToWorld(int x, int y);
    Vector3 SnapWorldToCellCenter(Vector3 world);
    void ReleaseById(int id);
    void OccupyRect(int x, int y, int rw, int rh, int id = 0);
    public int GetIdAt(int x, int y);
    bool IsAreaFreeRect(int x, int y, int w, int h);
    int AddBlockedRect(int x, int z, int w, int h);
    int RemoveBlockedRect(int x, int z, int w, int h);
}
