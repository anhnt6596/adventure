using System;
using System.Collections.Generic;
using UnityEngine;

// Layer index is the terrain id and its draw priority: a higher index draws over every lower one.
[Serializable]
public class TerrainLayer
{
    public string name;
    public bool walkable = true;

    [Tooltip("Editor-only: paints the map readable before any art exists.")]
    public Color previewColor = Color.magenta;

    [Tooltip("Indexed by which sides transition: bit0 N, bit1 E, bit2 S, bit3 W. 0 = plain.\n" +
             "DualGrid instead indexes corners: bit0 SW, bit1 SE, bit2 NW, bit3 NE.")]
    public Sprite[] tiles = new Sprite[SameGrid.MaskCount];
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

    public bool[] BuildLayerTable(int layer)
    {
        var table = new bool[256];
        for (int i = 0; i < table.Length; i++) table[i] = i >= layer;
        return table;
    }

    // Bodies carry a mask over terrain ids, so a swimmer or a buff opens water without the terrain
    // changing.
    public int BuildDefaultPassMask()
    {
        int mask = 0;
        for (int i = 0; i < layers.Count && i < 32; i++)
            if (layers[i].walkable) mask |= 1 << i;
        return mask;
    }

    public static int BitOf(int terrainId) => terrainId < 32 ? 1 << terrainId : 0;

    // Unity serializes a new list element without running field initialisers, so the arrays arrive
    // empty and their slots never appear in the Inspector.
    void OnValidate()
    {
        foreach (var layer in layers)
        {
            if (layer == null) continue;
            layer.tiles = Resize(layer.tiles, SameGrid.MaskCount);
        }
    }

    static Sprite[] Resize(Sprite[] source, int length)
    {
        if (source != null && source.Length == length) return source;

        var resized = new Sprite[length];
        if (source != null)
            for (int i = 0; i < source.Length && i < length; i++) resized[i] = source[i];
        return resized;
    }
}
