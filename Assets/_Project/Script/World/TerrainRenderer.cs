using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[RequireComponent(typeof(TerrainGrid))]
public class TerrainRenderer : MonoBehaviour
{
    public enum TileMode { SameGrid, Quadrant, DualGrid }

    const string LayerPrefix = "Layer_";
    const int InnerNW = 0, InnerNE = 1, InnerSE = 2, InnerSW = 3;

    [SerializeField] TileMode mode = TileMode.Quadrant;
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

        ClearLayers();

        var set = _grid.Set;
        if (set == null || set.Count == 0 || material == null) return;

        for (int layer = 0; layer < set.Count; layer++)
        {
            var mesh = BuildLayerMesh(layer, set);
            if (mesh == null) continue;

            var go = new GameObject($"{LayerPrefix}{layer}_{set.layers[layer].name}");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, layer * layerHeight, 0f);
            go.hideFlags = HideFlags.DontSave;

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MaterialFor(set.layers[layer]);
            mr.shadowCastingMode = ShadowCastingMode.Off;

            _layerObjects.Add(go);
        }
    }

    Mesh BuildLayerMesh(int layer, TerrainSet set)
    {
        var data = set.layers[layer];
        var map = _grid.Map;
        var inLayer = set.BuildLayerTable(layer);
        float cs = _grid.CellSize;

        _verts.Clear();
        _uvs.Clear();
        _tris.Clear();

        switch (mode)
        {
            case TileMode.SameGrid:
                for (int y = 0; y < map.Height; y++)
                    for (int x = 0; x < map.Width; x++)
                    {
                        if (layer > 0 && !inLayer[map.Get(x, y)]) continue;
                        int mask = layer == 0 ? 0 : SameGrid.NeighbourMask(map, x, y, inLayer);
                        AddQuad(x * cs, y * cs, cs, Tile(data, mask));
                    }
                break;

            case TileMode.Quadrant:
                for (int y = 0; y < map.Height; y++)
                    for (int x = 0; x < map.Width; x++)
                    {
                        if (layer > 0 && !inLayer[map.Get(x, y)]) continue;
                        int mask = layer == 0 ? 0 : SameGrid.NeighbourMask(map, x, y, inLayer);
                        int diag = layer == 0 ? 0 : SameGrid.DiagonalMask(map, x, y, inLayer);
                        AddQuarters(x * cs, y * cs, cs, data, mask, diag);
                    }
                break;

            case TileMode.DualGrid:
                for (int j = 0; j <= map.Height; j++)
                    for (int i = 0; i <= map.Width; i++)
                    {
                        int mask = layer == 0 ? 15 : DualGrid.CornerMask(map, i, j, inLayer);
                        if (mask == 0) continue;
                        AddQuad((i - 0.5f) * cs, (j - 0.5f) * cs, cs, Tile(data, mask));
                    }
                break;
        }

        if (_verts.Count == 0) return null;

        var mesh = new Mesh { name = $"Terrain_Layer_{layer}", indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(_verts);
        mesh.SetUVs(0, _uvs);
        mesh.SetTriangles(_tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    void AddQuarters(float x0, float z0, float cs, TerrainLayer data, int mask, int diag)
    {
        bool n = (mask & SameGrid.OpenNorth) != 0;
        bool e = (mask & SameGrid.OpenEast) != 0;
        bool s = (mask & SameGrid.OpenSouth) != 0;
        bool w = (mask & SameGrid.OpenWest) != 0;

        float h = cs * 0.5f;

        AddQuarter(x0, z0 + h, h, data, n, w, SameGrid.OpenNorth, SameGrid.OpenWest,
            (diag & SameGrid.DiagNorthWest) != 0, InnerNW, 0, 1);
        AddQuarter(x0 + h, z0 + h, h, data, n, e, SameGrid.OpenNorth, SameGrid.OpenEast,
            (diag & SameGrid.DiagNorthEast) != 0, InnerNE, 1, 1);
        AddQuarter(x0 + h, z0, h, data, s, e, SameGrid.OpenSouth, SameGrid.OpenEast,
            (diag & SameGrid.DiagSouthEast) != 0, InnerSE, 1, 0);
        AddQuarter(x0, z0, h, data, s, w, SameGrid.OpenSouth, SameGrid.OpenWest,
            (diag & SameGrid.DiagSouthWest) != 0, InnerSW, 0, 0);
    }

    void AddQuarter(float x0, float z0, float size, TerrainLayer data,
                    bool openA, bool openB, int bitA, int bitB,
                    bool diagOpen, int innerIndex, int qx, int qy)
    {
        bool concave = !openA && !openB && diagOpen;

        var sprite = concave
            ? Inner(data, innerIndex) ?? Tile(data, 0)
            : Tile(data, (openA ? bitA : 0) | (openB ? bitB : 0)) ?? Tile(data, 0);

        if (sprite == null) return;

        var r = sprite.textureRect;
        AddQuadUV(x0, z0, size, sprite.texture, new Rect(
            r.xMin + qx * r.width * 0.5f,
            r.yMin + qy * r.height * 0.5f,
            r.width * 0.5f,
            r.height * 0.5f));
    }

    static Sprite Tile(TerrainLayer data, int slot)
        => data.tiles != null && slot < data.tiles.Length ? data.tiles[slot] : null;

    static Sprite Inner(TerrainLayer data, int index)
        => data.innerCorners != null && index < data.innerCorners.Length ? data.innerCorners[index] : null;

    void AddQuad(float x0, float z0, float size, Sprite sprite)
    {
        if (sprite == null) return;
        AddQuadUV(x0, z0, size, sprite.texture, sprite.textureRect);
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

    // Mesh UVs address the atlas the layer's sprites live in, so each layer needs a material
    // pointing at that texture.
    Material MaterialFor(TerrainLayer data)
    {
        var texture = AtlasOf(data);
        if (texture == null || texture == material.mainTexture) return material;

        var copy = new Material(material) { name = $"{material.name}_{data.name}", hideFlags = HideFlags.DontSave };
        copy.mainTexture = texture;
        _layerMaterials.Add(copy);
        return copy;
    }

    static Texture2D AtlasOf(TerrainLayer data)
    {
        if (data.tiles != null)
            foreach (var sprite in data.tiles)
                if (sprite != null) return sprite.texture;
        return null;
    }

    // A domain reload clears the lists but leaves the objects, so children are scanned rather than
    // tracked.
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
