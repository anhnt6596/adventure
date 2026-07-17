using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// One mesh per terrain layer, stacked bottom-up. A 64x64 map is ~4k display tiles per layer, so
// spawning a SpriteRenderer each is not an option; the tiles share an atlas and bake into one mesh.
[ExecuteAlways]
[RequireComponent(typeof(TerrainGrid))]
public class TerrainRenderer : MonoBehaviour
{
    // Both modes read 16 tiles and stack identically; they differ only in which tile a cell picks.
    // SameGrid fits ready-made tilesets, DualGrid fits art authored for it - so the choice belongs
    // to the map, following whatever art it uses.
    public enum TileMode { SameGrid, Quadrant, DualGrid }

    const string LayerPrefix = "Layer_";

    [Tooltip("SameGrid: one tile per cell - needs art for every case.\n" +
             "Quadrant: each cell built from four quarters of the same 9 tiles, so strips and lone " +
             "cells work without extra art.\n" +
             "DualGrid: for art authored by the tileset generator.")]
    [SerializeField] TileMode mode = TileMode.Quadrant;

    [Tooltip("Template. Each layer gets a copy with its own atlas, since layers rarely share one.")]
    [SerializeField] Material material;

    [Tooltip("Vertical gap between layers. Coplanar layers z-fight.")]
    [SerializeField] float layerHeight = 0.002f;

    TerrainGrid _grid;
    readonly List<GameObject> _layerObjects = new List<GameObject>();
    readonly List<Material> _layerMaterials = new List<Material>();

    readonly List<Vector3> _verts = new List<Vector3>();
    readonly List<Vector2> _uvs = new List<Vector2>();
    readonly List<int> _tris = new List<int>();

    void Awake() => _grid = GetComponent<TerrainGrid>();
    void OnEnable() { _grid = GetComponent<TerrainGrid>(); Build(); }

    [ContextMenu("Rebuild Terrain Mesh")]
    public void Build()
    {
        if (_grid == null) _grid = GetComponent<TerrainGrid>();
        var set = _grid.Set;
        if (set == null || set.Count == 0) return;

        ClearLayers();

        if (material == null)
        {
            Debug.LogWarning("[Terrain] No material assigned - nothing will render.", this);
            return;
        }

        int drawn = 0;
        for (int layer = 0; layer < set.Count; layer++)
        {
            var mesh = BuildLayerMesh(layer, set);
            if (mesh == null) continue;
            drawn++;

            var go = new GameObject($"{LayerPrefix}{layer}_{set.layers[layer].name}");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, layer * layerHeight, 0f);
            go.hideFlags = HideFlags.DontSave;

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MaterialFor(layer, set);
            mr.shadowCastingMode = ShadowCastingMode.Off;

            _layerObjects.Add(go);
        }

        // Silence here reads as a broken renderer; it is almost always just unassigned tiles.
        if (drawn == 0)
            Debug.LogWarning(
                $"[Terrain] '{set.name}' has no tiles assigned, so nothing rendered. Fill each " +
                "layer's Tiles array. Painting still works via Gizmos.",
                this);
    }

    // The mesh UVs address the atlas the layer's sprites live in, so the material has to point at
    // that same texture. Layers with separate atlases therefore need separate materials.
    Material MaterialFor(int layer, TerrainSet set)
    {
        if (material == null) return null;

        var texture = AtlasOf(set.layers[layer]);
        if (texture == null || texture == material.mainTexture) return material;

        var copy = new Material(material) { name = $"{material.name}_{layer}", hideFlags = HideFlags.DontSave };
        copy.mainTexture = texture;
        _layerMaterials.Add(copy);
        return copy;
    }

    static Texture2D AtlasOf(TerrainLayer layer)
    {
        foreach (var sprite in layer.tiles)
            if (sprite != null) return sprite.texture;
        return null;
    }

    Mesh BuildLayerMesh(int layer, TerrainSet set)
    {
        var tiles = set.layers[layer].tiles;
        if (tiles == null || tiles.Length < SameGrid.MaskCount) return null;

        var map = _grid.Map;
        var inLayer = set.BuildLayerTable(layer);
        float cs = _grid.CellSize;

        _verts.Clear();
        _uvs.Clear();
        _tris.Clear();

        if (mode == TileMode.SameGrid)
        {
            // One quad per cell, aligned to it.
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                {
                    // The base layer covers everything; higher layers only draw on their own cells.
                    if (layer > 0 && !inLayer[map.Get(x, y)]) continue;

                    // The base layer has nothing to transition against: mask 0 is its plain tile.
                    int mask = layer == 0 ? 0 : SameGrid.NeighbourMask(map, x, y, inLayer);
                    var sprite = tiles[mask];
                    if (sprite == null) continue;

                    AddQuad(x * cs, y * cs, cs, sprite);
                }
        }
        else if (mode == TileMode.Quadrant)
        {
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                {
                    if (layer > 0 && !inLayer[map.Get(x, y)]) continue;

                    int mask = layer == 0 ? 0 : SameGrid.NeighbourMask(map, x, y, inLayer);
                    int diag = layer == 0 ? 0 : SameGrid.DiagonalMask(map, x, y, inLayer);
                    AddCellQuadrants(x * cs, y * cs, cs, set.layers[layer], mask, diag);
                }
        }
        else
        {
            // One quad per cell corner, offset half a cell, so (W+1)x(H+1) of them.
            for (int j = 0; j <= map.Height; j++)
                for (int i = 0; i <= map.Width; i++)
                {
                    // DualGrid reads the opposite way round: 15 is all four corners in-layer.
                    int mask = layer == 0 ? 15 : DualGrid.CornerMask(map, i, j, inLayer);
                    if (mask == 0) continue;

                    var sprite = tiles[mask];
                    if (sprite == null) continue;

                    AddQuad((i - 0.5f) * cs, (j - 0.5f) * cs, cs, sprite);
                }
        }

        if (_verts.Count == 0) return null;

        var mesh = new Mesh { name = $"Terrain_Layer_{layer}", indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(_verts);
        mesh.SetUVs(0, _uvs);
        mesh.SetTriangles(_tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // A cell's quarter depends only on its own two sides plus the diagonal between them, so each
    // quarter can come from a different tile. Strips and lone cells fall out of the same nine tiles
    // a 3x3 set draws, with no whole-tile art for those cases needed.
    void AddCellQuadrants(float x0, float z0, float cs, TerrainLayer layer, int mask, int diag)
    {
        bool n = (mask & SameGrid.OpenNorth) != 0;
        bool e = (mask & SameGrid.OpenEast) != 0;
        bool s = (mask & SameGrid.OpenSouth) != 0;
        bool w = (mask & SameGrid.OpenWest) != 0;

        float h = cs * 0.5f;

        AddQuarter(x0, z0 + h, h, layer, n, w, SameGrid.OpenNorth, SameGrid.OpenWest,
            (diag & SameGrid.DiagNorthWest) != 0, InnerNW, 0, 1);
        AddQuarter(x0 + h, z0 + h, h, layer, n, e, SameGrid.OpenNorth, SameGrid.OpenEast,
            (diag & SameGrid.DiagNorthEast) != 0, InnerNE, 1, 1);
        AddQuarter(x0 + h, z0, h, layer, s, e, SameGrid.OpenSouth, SameGrid.OpenEast,
            (diag & SameGrid.DiagSouthEast) != 0, InnerSE, 1, 0);
        AddQuarter(x0, z0, h, layer, s, w, SameGrid.OpenSouth, SameGrid.OpenWest,
            (diag & SameGrid.DiagSouthWest) != 0, InnerSW, 0, 0);
    }

    const int InnerNW = 0, InnerNE = 1, InnerSE = 2, InnerSW = 3;

    // The five cases a quarter can be in. Inner corner is the one a 3x3 tileset has no art for, so
    // it degrades to the plain quarter - drop an inner-corner sprite in and it is used, with no
    // code change.
    void AddQuarter(float x0, float z0, float size, TerrainLayer layer,
                    bool openA, bool openB, int bitA, int bitB,
                    bool diagOpen, int innerIndex, int qx, int qy)
    {
        bool concave = !openA && !openB && diagOpen;

        var sprite = concave
            ? Inner(layer, innerIndex)
            : Tile(layer, (openA ? bitA : 0) | (openB ? bitB : 0));

        AddSubQuad(x0, z0, size, sprite ?? Tile(layer, 0), qx, qy);
    }

    static Sprite Tile(TerrainLayer layer, int slot)
        => layer.tiles != null && slot < layer.tiles.Length ? layer.tiles[slot] : null;

    static Sprite Inner(TerrainLayer layer, int index)
        => layer.innerCorners != null && index < layer.innerCorners.Length ? layer.innerCorners[index] : null;

    void AddSubQuad(float x0, float z0, float size, Sprite sprite, int qx, int qy)
    {
        if (sprite == null) return;

        var rect = sprite.textureRect;
        var half = new Rect(
            rect.xMin + qx * rect.width * 0.5f,
            rect.yMin + qy * rect.height * 0.5f,
            rect.width * 0.5f,
            rect.height * 0.5f);

        AddQuadUV(x0, z0, size, sprite.texture, half);
    }

    // Takes the quad's corner in grid-local units: the modes place quads differently, so the caller
    // decides where and this only builds.
    void AddQuad(float x0, float z0, float cellSize, Sprite sprite)
    {
        if (sprite == null) return;
        AddQuadUV(x0, z0, cellSize, sprite.texture, sprite.textureRect);
    }

    void AddQuadUV(float x0, float z0, float size, Texture tex, Rect pixelRect)
    {
        float x1 = x0 + size;
        float z1 = z0 + size;

        int v = _verts.Count;
        _verts.Add(new Vector3(x0, 0f, z0));
        _verts.Add(new Vector3(x1, 0f, z0));
        _verts.Add(new Vector3(x0, 0f, z1));
        _verts.Add(new Vector3(x1, 0f, z1));

        float u0 = pixelRect.xMin / tex.width, u1 = pixelRect.xMax / tex.width;
        float w0 = pixelRect.yMin / tex.height, w1 = pixelRect.yMax / tex.height;

        _uvs.Add(new Vector2(u0, w0));
        _uvs.Add(new Vector2(u1, w0));
        _uvs.Add(new Vector2(u0, w1));
        _uvs.Add(new Vector2(u1, w1));

        _tris.Add(v); _tris.Add(v + 2); _tris.Add(v + 1);
        _tris.Add(v + 1); _tris.Add(v + 2); _tris.Add(v + 3);
    }

    // Scans children rather than trusting the list: an editor domain reload wipes the list but
    // leaves the objects behind, and rebuilding would then stack duplicate layers.
    void ClearLayers()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (!child.name.StartsWith(LayerPrefix)) continue;

            var filter = child.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null) DestroySafe(filter.sharedMesh);
            DestroySafe(child.gameObject);
        }
        _layerObjects.Clear();

        // Build() runs on every paint stroke, so leaking a copy per layer per stroke adds up fast.
        foreach (var m in _layerMaterials)
            if (m != null) DestroySafe(m);
        _layerMaterials.Clear();
    }

    static void DestroySafe(Object obj)
    {
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }
}
