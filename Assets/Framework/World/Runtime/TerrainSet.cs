using System;
using System.Collections.Generic;
using UnityEngine;

// What a terrain is for movement. Only Land is walkable; the walkable boundary is baked from it.
// Water and None both block (a body's pass mask can open water for a swimmer).
public enum TerrainKind { None, Land, Water }

// Layer index is the terrain id and its draw priority: a higher index draws over every lower one.
[Serializable]
public class TerrainLayer
{
    public string name;
    public TerrainKind kind = TerrainKind.Land;

    [Tooltip("Editor-only: paints the map readable before any art exists.")]
    public Color previewColor = Color.magenta;

    [Tooltip("The four autotile pieces: 0 = full, 1 = edge, 2 = outer corner, 3 = inner corner. " +
             "Authored for one reference orientation; the renderer rotates them to fit.")]
    public Sprite[] tiles = new Sprite[4];
}

[CreateAssetMenu(menuName = "World/Terrain Set")]
public class TerrainSet : ScriptableObject
{
    public List<TerrainLayer> layers = new List<TerrainLayer>();

    public int Count => layers.Count;

    public bool IsWalkable(byte id) => id < layers.Count && layers[id].kind == TerrainKind.Land;

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
            if (layers[i].kind == TerrainKind.Land) mask |= 1 << i;
        return mask;
    }

    public static int BitOf(int terrainId) => terrainId < 32 ? 1 << terrainId : 0;

    // Unity serializes a new list element without running field initialisers, so a new layer's array
    // arrives null; give it the four piece slots.
    void OnValidate()
    {
        foreach (var layer in layers)
            if (layer != null && (layer.tiles == null || layer.tiles.Length == 0))
                layer.tiles = new Sprite[4];
    }
}
