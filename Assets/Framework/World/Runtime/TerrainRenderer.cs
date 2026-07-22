using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[RequireComponent(typeof(TerrainGrid))]
public class TerrainRenderer : MonoBehaviour
{
    const string LayerPrefix = "Layer_";

    [SerializeField] Material material;
    [Tooltip("Order of the topmost terrain layer; the ones below it count down. Keep it under " +
             "anything that stands on the ground.")]
    [SerializeField] int sortingOrder = -2;

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

#if UNITY_EDITOR
    // Layer and sorting order are copied onto the generated objects, so changing them here has to
    // reach the ones already built.
    void OnValidate()
    {
        for (int i = 0; i < _layerObjects.Count; i++)
        {
            var go = _layerObjects[i];
            if (go == null) continue;
            go.layer = gameObject.layer;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = sortingOrder - (_layerObjects.Count - 1 - i);
        }
    }
#endif

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

            // Rebuilt from scratch on every paint stroke and reload, so nothing set on them by hand
            // survives - anything they need is taken from this component or its GameObject.
            var go = new GameObject($"{LayerPrefix}{layer}_{set.layers[layer].name}");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, layer * layerHeight, 0f);
            go.hideFlags = HideFlags.DontSave;
            go.layer = gameObject.layer;

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = MaterialsForSubmeshes(layer);
            mr.shadowCastingMode = ShadowCastingMode.Off;

            // The material writes no depth and the layers overlap (a terrain overhangs onto the ones
            // below), so a distinct render queue per layer forces the draw order - a higher terrain
            // always paints after, and over, everything under it. sortingOrder is a fallback.
            mr.sortingOrder = sortingOrder - (set.Count - 1 - layer);

            _layerObjects.Add(go);
        }
    }

    // Four base pieces (tiles[0..3]) authored for one reference orientation, rotated to cover every case.
    const int PieceFull = 0, PieceEdge = 1, PieceOuter = 2, PieceInner = 3;

    Mesh BuildLayerMesh(int layer, TerrainSet set)
    {
        var data = set.layers[layer];
        var inLayer = set.BuildLayerTable(layer);

        // Upscale the map x2 so a terrain overflows a half-cell onto its neighbours; one fine cell = one
        // tile. Terrain cells are full; each empty cell the terrain touches gets one overflow piece.
        var grid = _grid.Map.Subdivide(2);
        float size = _grid.CellSize * 0.5f;

        _verts.Clear();
        _uvs.Clear();
        _textures.Clear();
        _trisPerTexture.Clear();

        for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
            {
                float x0 = x * size, y0 = y * size;

                if (inLayer[grid.Get(x, y)])
                {
                    AddRotatedQuad(x0, y0, size, Tile(data, PieceFull), 0);
                    continue;
                }

                bool n = inLayer[grid.Get(x, y + 1)];
                bool s = inLayer[grid.Get(x, y - 1)];
                bool e = inLayer[grid.Get(x + 1, y)];
                bool w = inLayer[grid.Get(x - 1, y)];
                bool ne = inLayer[grid.Get(x + 1, y + 1)];
                bool nw = inLayer[grid.Get(x - 1, y + 1)];
                bool se = inLayer[grid.Get(x + 1, y - 1)];
                bool sw = inLayer[grid.Get(x - 1, y - 1)];

                if (Overflow(n, e, s, w, ne, nw, se, sw, out int piece, out int rot))
                    AddRotatedQuad(x0, y0, size, Tile(data, piece), rot);
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

    // For an empty cell, pick the overflow piece + CCW quarter-turn from where the terrain touches it:
    // one side -> straight edge, two adjacent sides -> concave inner corner, a lone diagonal -> convex
    // outer corner. Rotations are relative to the pieces authored for N / NE.
    static bool Overflow(bool n, bool e, bool s, bool w, bool ne, bool nw, bool se, bool sw, out int piece, out int rot)
    {
        piece = 0;
        rot = 0;

        int sides = (n ? 1 : 0) + (e ? 1 : 0) + (s ? 1 : 0) + (w ? 1 : 0);

        if (sides >= 3) { piece = PieceFull; return true; }   // nearly enclosed

        if (sides == 2)
        {
            if (n && e) { piece = PieceInner; rot = 2; return true; }
            if (n && w) { piece = PieceInner; rot = 3; return true; }
            if (s && w) { piece = PieceInner; rot = 0; return true; }
            if (s && e) { piece = PieceInner; rot = 1; return true; }
            // opposite sides (N&S / E&W): a one-cell channel, fall through to an edge
        }

        if (sides >= 1)
        {
            // Base edge art has the terrain along the south, so rotate by which side the terrain is on.
            piece = PieceEdge;
            rot = s ? 0 : e ? 1 : n ? 2 : 3;
            return true;
        }

        // Lone diagonal: base outer art has the terrain nub in the SW, rotate by the terrain corner.
        if (sw) { piece = PieceOuter; rot = 0; return true; }
        if (se) { piece = PieceOuter; rot = 1; return true; }
        if (ne) { piece = PieceOuter; rot = 2; return true; }
        if (nw) { piece = PieceOuter; rot = 3; return true; }

        return false;   // terrain not adjacent → nothing
    }

    // Adds one quad, turning the sprite by rot quarter-turns CCW (0..3) by mapping the sprite's corners
    // onto the fixed quad vertices. UV comes from the full authored slice so it matches the art 1:1.
    void AddRotatedQuad(float x0, float z0, float size, Sprite sprite, int rot)
    {
        if (sprite == null) return;

        var tex = sprite.texture;
        var pr = sprite.rect;   // full authored slice, not textureRect (which trims transparent borders)
        float x1 = x0 + size, z1 = z0 + size;

        int v = _verts.Count;
        _verts.Add(new Vector3(x0, 0f, z0));
        _verts.Add(new Vector3(x1, 0f, z0));
        _verts.Add(new Vector3(x0, 0f, z1));
        _verts.Add(new Vector3(x1, 0f, z1));

        float u0 = pr.xMin / tex.width, u1 = pr.xMax / tex.width;
        float w0 = pr.yMin / tex.height, w1 = pr.yMax / tex.height;

        // Sprite corners CCW from bottom-left; a CCW turn shifts which one lands on each vertex.
        Span<Vector2> c = stackalloc Vector2[4]
        {
            new Vector2(u0, w0), new Vector2(u1, w0), new Vector2(u1, w1), new Vector2(u0, w1)
        };
        int r = ((rot % 4) + 4) % 4;
        Vector2 uvBL = c[(0 - r + 4) % 4];
        Vector2 uvBR = c[(1 - r + 4) % 4];
        Vector2 uvTR = c[(2 - r + 4) % 4];
        Vector2 uvTL = c[(3 - r + 4) % 4];

        _uvs.Add(uvBL);
        _uvs.Add(uvBR);
        _uvs.Add(uvTL);
        _uvs.Add(uvTR);

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

    Material[] MaterialsForSubmeshes(int layer)
    {
        var materials = new Material[_textures.Count];
        for (int i = 0; i < _textures.Count; i++)
        {
            var copy = new Material(material) { name = $"{material.name}_{_textures[i].name}", hideFlags = HideFlags.DontSave };
            copy.mainTexture = _textures[i];
            copy.renderQueue = material.renderQueue + layer;   // higher terrain draws after, and over, lower
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

    static void DestroySafe(UnityEngine.Object obj)
    {
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }
}
