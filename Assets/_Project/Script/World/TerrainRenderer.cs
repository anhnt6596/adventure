using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// One mesh per terrain layer, stacked bottom-up. A 64x64 map is ~4k display tiles per layer, so
// spawning a SpriteRenderer each is not an option; the tiles share an atlas and bake into one mesh.
[ExecuteAlways]
[RequireComponent(typeof(TerrainGrid))]
public class TerrainRenderer : MonoBehaviour
{
    const string LayerPrefix = "Layer_";

    [SerializeField] Material material;

    [Tooltip("Vertical gap between layers. Coplanar layers z-fight.")]
    [SerializeField] float layerHeight = 0.002f;

    TerrainGrid _grid;
    readonly List<GameObject> _layerObjects = new List<GameObject>();

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
            mr.sharedMaterial = material;
            mr.shadowCastingMode = ShadowCastingMode.Off;

            _layerObjects.Add(go);
        }
    }

    Mesh BuildLayerMesh(int layer, TerrainSet set)
    {
        var tiles = set.layers[layer].tiles;
        if (tiles == null || tiles.Length < DualGrid.MaskCount) return null;

        var map = _grid.Map;
        var inLayer = set.BuildLayerTable(layer);
        float cs = _grid.CellSize;

        _verts.Clear();
        _uvs.Clear();
        _tris.Clear();

        for (int j = 0; j <= map.Height; j++)
            for (int i = 0; i <= map.Width; i++)
            {
                // The base layer fills everything; higher layers only draw where they exist.
                int mask = layer == 0 ? 15 : DualGrid.CornerMask(map, i, j, inLayer);
                if (mask == 0) continue;

                var sprite = tiles[mask];
                if (sprite == null) continue;

                AddQuad(i, j, cs, sprite);
            }

        if (_verts.Count == 0) return null;

        var mesh = new Mesh { name = $"Terrain_Layer_{layer}", indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(_verts);
        mesh.SetUVs(0, _uvs);
        mesh.SetTriangles(_tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    void AddQuad(int i, int j, float cellSize, Sprite sprite)
    {
        float x0 = (i - 0.5f) * cellSize, x1 = (i + 0.5f) * cellSize;
        float z0 = (j - 0.5f) * cellSize, z1 = (j + 0.5f) * cellSize;

        int v = _verts.Count;
        _verts.Add(new Vector3(x0, 0f, z0));
        _verts.Add(new Vector3(x1, 0f, z0));
        _verts.Add(new Vector3(x0, 0f, z1));
        _verts.Add(new Vector3(x1, 0f, z1));

        var rect = sprite.textureRect;
        var tex = sprite.texture;
        float u0 = rect.xMin / tex.width, u1 = rect.xMax / tex.width;
        float w0 = rect.yMin / tex.height, w1 = rect.yMax / tex.height;

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
    }

    static void DestroySafe(Object obj)
    {
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }
}
