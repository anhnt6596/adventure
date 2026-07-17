using System;
using System.Collections.Generic;
using UnityEngine;

// Layer index is the terrain id AND its draw priority: a higher index draws over every lower one,
// so each terrain only needs its own edges (N tile sets, not N^2 pairwise transitions).
[Serializable]
public class TerrainLayer
{
    public string name;
    public bool walkable = true;

    [Tooltip("Editor-only: paints the map readable before any art exists.")]
    public Color previewColor = Color.magenta;

    [Tooltip("16 tiles indexed by dual-grid corner mask: bit0 SW, bit1 SE, bit2 NW, bit3 NE")]
    public Sprite[] tiles = new Sprite[16];
}

[CreateAssetMenu(menuName = "World/Terrain Set")]
public class TerrainSet : ScriptableObject
{
    public List<TerrainLayer> layers = new List<TerrainLayer>();

    public int Count => layers.Count;

    public bool IsWalkable(byte id) => id < layers.Count && layers[id].walkable;

    public bool[] BuildWalkableTable()
    {
        var table = new bool[256];
        for (int i = 0; i < table.Length; i++) table[i] = IsWalkable((byte)i);
        return table;
    }

    // Passability is a property of (terrain, body), not of terrain alone: a crocodile swims, a bird
    // flies over, and a buff can open water for five seconds. Bodies carry a mask over terrain ids;
    // this is the default one - everything the set calls walkable.
    public int BuildDefaultPassMask()
    {
        int mask = 0;
        for (int i = 0; i < layers.Count && i < 32; i++)
            if (layers[i].walkable) mask |= 1 << i;
        return mask;
    }

    public static int BitOf(int terrainId) => terrainId < 32 ? 1 << terrainId : 0;

    // inLayer[id] == true when a cell of terrain `id` counts as filled for this layer.
    public bool[] BuildLayerTable(int layer)
    {
        var table = new bool[256];
        for (int i = 0; i < table.Length; i++) table[i] = i >= layer;
        return table;
    }
}
