using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[RequireComponent(typeof(TerrainGrid))]
public class TerrainRenderer : MonoBehaviour
{
    public enum TileMode { SameGrid, Quadrant, DualGrid }

    const string LayerPrefix = "Layer_";

    [SerializeField] TileMode mode = TileMode.Quadrant;
    [SerializeField] Material material;

    [Tooltip("Vertical gap between layers. Coplanar layers z-fight.")]
    [SerializeField] float layerHeight = 0.002f;

    TerrainGrid _grid;
    readonly List<GameObject> _layerObjects = new List<GameObject>();
    readonly List<Material> _layerMaterials = new List<Material>();

    readonly List<Vector3> _verts = new List<Vector3>();
    readonly List<Vector2> _uvs = new List<Vector2>();

    // A tileset of loose PNGs gives every tile its own texture, and one mesh can only bind one per
    // submesh - so triangles are grouped by the texture their UVs address.
    readonly List<Texture2D> _textures = new List<Texture2D>();
    readonly List<List<int>> _trisPerTexture = new List<List<int>>();

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
            mr.sharedMaterials = MaterialsForSubmeshes();
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
        _textures.Clear();
        _trisPerTexture.Clear();

        if (mode == TileMode.DualGrid)
        {
            for (int j = 0; j <= map.Height; j++)
                for (int i = 0; i <= map.Width; i++)
                {
                    int mask = layer == 0 ? 15 : DualGrid.CornerMask(map, i, j, inLayer);
                    if (mask == 0) continue;
                    AddQuad((i - 0.5f) * cs, (j - 0.5f) * cs, cs, Tile(data, mask));
                }
        }
        else
        {
            // Quadrant is SameGrid over a map split 2x2: same rule, finer input.
            int step = mode == TileMode.Quadrant ? 2 : 1;
            var grid = step == 1 ? map : map.Subdivide(step);
            float size = cs / step;

            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    if (layer > 0 && !inLayer[grid.Get(x, y)]) continue;
                    int mask = layer == 0 ? 0 : SameGrid.NeighbourMask(grid, x, y, inLayer);
                    AddQuad(x * size, y * size, size, Tile(data, mask));
                }
        }

        if (_verts.Count == 0) return null;

        var mesh = new Mesh { name = $"Terrain_Layer_{layer}", indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(_verts);
        mesh.SetUVs(0, _uvs);
        mesh.subMeshCount = _trisPerTexture.Count;
        for (int i = 0; i < _trisPerTexture.Count; i++)
            mesh.SetTriangles(_trisPerTexture[i], i);
        mesh.RecalculateBounds();
        return mesh;
    }

    static Sprite Tile(TerrainLayer data, int slot)
        => data.tiles != null && slot < data.tiles.Length ? data.tiles[slot] : null;

    void AddQuad(float x0, float z0, float size, Sprite sprite)
    {
        if (sprite == null) return;
        AddQuadUV(x0, z0, size, sprite.texture, sprite.textureRect);
    }

    void AddQuadUV(float x0, float z0, float size, Texture2D tex, Rect pixelRect)
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

        var tris = TrianglesFor(tex);
        tris.Add(v); tris.Add(v + 2); tris.Add(v + 1);
        tris.Add(v + 1); tris.Add(v + 2); tris.Add(v + 3);
    }

    List<int> TrianglesFor(Texture2D tex)
    {
        int i = _textures.IndexOf(tex);
        if (i < 0)
        {
            _textures.Add(tex);
            _trisPerTexture.Add(new List<int>());
            i = _textures.Count - 1;
        }
        return _trisPerTexture[i];
    }

    Material[] MaterialsForSubmeshes()
    {
        var materials = new Material[_textures.Count];
        for (int i = 0; i < _textures.Count; i++)
        {
            var copy = new Material(material) { name = $"{material.name}_{_textures[i].name}", hideFlags = HideFlags.DontSave };
            copy.mainTexture = _textures[i];
            _layerMaterials.Add(copy);
            materials[i] = copy;
        }
        return materials;
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
