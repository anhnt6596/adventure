using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[RequireComponent(typeof(TerrainGrid))]
public class TerrainRenderer : MonoBehaviour
{
    const string LayerPrefix = "Layer_";
    const string WaterPrefix = "Water_";   // named apart so a tile-only rebuild can leave the water alone

    [SerializeField] Material material;
    [SerializeField] Material waterMaterial;   // a Water-kind layer is a flat mesh with this shader
    [Tooltip("Order of the topmost terrain layer; the ones below it count down. Keep it under " +
             "anything that stands on the ground.")]
    [SerializeField] int sortingOrder = -2;

    [Tooltip("Vertical gap between layers. Coplanar layers z-fight.")]
    [SerializeField] float layerHeight = 0.002f;

    [SerializeField, HideInInspector] bool baked;   // Rebuild Mesh saved the meshes; the game skips Build

    TerrainGrid _grid;
    HideFlags _genFlags = HideFlags.DontSave;   // generated objects: DontSave when live, None when baking
    readonly List<GameObject> _layerObjects = new List<GameObject>();
    readonly List<Material> _layerMaterials = new List<Material>();

    readonly List<Vector3> _verts = new List<Vector3>();
    readonly List<Vector2> _uvs = new List<Vector2>();

    // A tileset of loose PNGs gives every tile its own texture, and one mesh can only bind one per
    // submesh - so triangles are grouped by the texture their UVs address.
    readonly List<Texture2D> _textures = new List<Texture2D>();
    readonly List<List<int>> _trisPerTexture = new List<List<int>>();

    void Awake() => _grid = GetComponent<TerrainGrid>();

    // Baked meshes ride with the prefab, so the game (and reloads) skip the build entirely.
    void OnEnable() { _grid = GetComponent<TerrainGrid>(); if (!baked) Build(true, false); }

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

    // Live rebuild (DontSave) - used while editing and, if nothing is baked, on load.
    public void Build() => Build(true, false);
    public void Build(bool includeWater) => Build(includeWater, false);

    // Rebuild and bake the meshes into saved objects so they ride with the prefab and the game skips the
    // build. Any later edit (paint) drops back to a live rebuild until this runs again.
    [ContextMenu("Rebuild Terrain Mesh")]
    public void Bake()
    {
        Build(true, true);
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorUtility.SetDirty(this);
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null) UnityEditor.EditorUtility.SetDirty(stage.prefabContentsRoot);
            else UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    // includeWater = false skips the (slow) water mesh so a paint stroke rebuilds only the tiles; the
    // existing water surface is left in place and rebuilt once, when painting stops. bake = true saves the
    // generated objects with the prefab instead of leaving them throwaway.
    public void Build(bool includeWater, bool bake)
    {
        if (_grid == null) _grid = GetComponent<TerrainGrid>();

        baked = bake;
        _genFlags = bake ? HideFlags.None : HideFlags.DontSave;

        ClearLayers(includeWater);

        var set = _grid.Set;
        if (set == null || set.Count == 0 || material == null) return;

        for (int layer = 0; layer < set.Count; layer++)
        {
            if (set.layers[layer].kind == TerrainKind.Water)
            {
                if (includeWater) SpawnWaterLayer(layer, set);
                continue;
            }

            var mesh = BuildLayerMesh(layer, set);
            if (mesh == null) continue;
            mesh.hideFlags = _genFlags;

            var go = new GameObject($"{LayerPrefix}{layer}_{set.layers[layer].name}");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, layer * layerHeight, 0f);
            go.hideFlags = _genFlags;
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

    // A Water layer is a flat quad mesh over its exposed cells, drawn by the water shader (no tiles).
    void SpawnWaterLayer(int layer, TerrainSet set)
    {
        if (waterMaterial == null) return;

        var mesh = BuildWaterMesh(layer);
        if (mesh == null) return;
        mesh.hideFlags = _genFlags;

        var go = new GameObject($"{WaterPrefix}{layer}_{set.layers[layer].name}");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, layer * layerHeight, 0f);
        go.hideFlags = _genFlags;
        go.layer = gameObject.layer;

        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = waterMaterial;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.sortingOrder = sortingOrder - (set.Count - 1 - layer);

        _layerObjects.Add(go);
    }

    Mesh BuildWaterMesh(int layer)
    {
        var map = _grid.Map;
        float cs = _grid.CellSize;
        int w = map.Width, h = map.Height;

        // Shore distance is to the real water/land cell boundary (distance to the nearest land cell). A
        // 1-quad-per-cell mesh interpolates across the corner creases and the rim cuts them diagonally, so
        // subdivide; distances are exact per sub-vertex, cached since neighbouring sub-quads share them.
        const int sub = 4;
        float ss = cs / sub;
        var shoreCache = new Dictionary<int, float>();
        int stride = w * sub + 8;
        float Shore(int fi, int fj)
        {
            int key = fj * stride + fi;
            if (!shoreCache.TryGetValue(key, out float d))
                shoreCache[key] = d = LandDist(map, layer, (float)fi / sub, (float)fj / sub, 12) * cs;
            return d;
        }

        _verts.Clear();
        _uvs.Clear();
        var uv1 = new List<Vector2>();            // x = world distance to shore
        var tris = new List<int>();

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (map.Get(x, y) != layer) continue;   // water shows only where it's the top terrain

                for (int sj = 0; sj < sub; sj++)
                    for (int si = 0; si < sub; si++)
                    {
                        int fi = x * sub + si, fj = y * sub + sj;
                        float x0 = fi * ss, x1 = x0 + ss, z0 = fj * ss, z1 = z0 + ss;
                        int v = _verts.Count;
                        _verts.Add(new Vector3(x0, 0f, z0));
                        _verts.Add(new Vector3(x1, 0f, z0));
                        _verts.Add(new Vector3(x0, 0f, z1));
                        _verts.Add(new Vector3(x1, 0f, z1));
                        _uvs.Add(new Vector2(0, 0)); _uvs.Add(new Vector2(1, 0));
                        _uvs.Add(new Vector2(0, 1)); _uvs.Add(new Vector2(1, 1));
                        uv1.Add(new Vector2(Shore(fi, fj), 0));
                        uv1.Add(new Vector2(Shore(fi + 1, fj), 0));
                        uv1.Add(new Vector2(Shore(fi, fj + 1), 0));
                        uv1.Add(new Vector2(Shore(fi + 1, fj + 1), 0));
                        tris.Add(v); tris.Add(v + 2); tris.Add(v + 1);
                        tris.Add(v + 1); tris.Add(v + 2); tris.Add(v + 3);
                    }
            }

        if (_verts.Count == 0) return null;

        var mesh = new Mesh { name = $"Water_Layer_{layer}", indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(_verts);
        mesh.SetUVs(0, _uvs);
        mesh.SetUVs(1, uv1);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // Distance (in cells) from a point (cell units) to the nearest land cell - i.e. to the real
    // water/land boundary, since that boundary is the edge of the nearest land square. Searched within a
    // radius and capped at it, so deep water past the radius reads uniformly deep.
    static float LandDist(TerrainMap map, int waterLayer, float px, float pz, int radius)
    {
        int cx = Mathf.FloorToInt(px), cy = Mathf.FloorToInt(pz);
        float best = radius;
        for (int y = cy - radius; y <= cy + radius; y++)
            for (int x = cx - radius; x <= cx + radius; x++)
            {
                if (!map.InBounds(x, y) || map.Get(x, y) == waterLayer) continue;   // land only
                float ddx = Mathf.Max(0f, Mathf.Max(x - px, px - (x + 1)));
                float ddz = Mathf.Max(0f, Mathf.Max(y - pz, pz - (y + 1)));
                float d = Mathf.Sqrt(ddx * ddx + ddz * ddz);
                if (d < best) best = d;
            }
        return best;
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
            var copy = new Material(material) { name = $"{material.name}_{_textures[i].name}", hideFlags = _genFlags };
            copy.mainTexture = _textures[i];
            copy.renderQueue = material.renderQueue + layer;   // higher terrain draws after, and over, lower
            _layerMaterials.Add(copy);
            materials[i] = copy;
        }
        return materials;
    }

    // A domain reload clears the lists but leaves the objects, so children are scanned rather than
    // tracked.
    void ClearLayers(bool includeWater)
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            bool water = child.name.StartsWith(WaterPrefix);
            if (!child.name.StartsWith(LayerPrefix) && !water) continue;
            if (water && !includeWater) continue;   // keep the water surface across a paint stroke

            var filter = child.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null) DestroySafe(filter.sharedMesh);

            // Free the generated material copies too (baked ones aren't tracked after a reload).
            var mr = child.GetComponent<MeshRenderer>();
            if (mr != null)
                foreach (var m in mr.sharedMaterials)
                    if (m != null && m != material && m != waterMaterial) DestroySafe(m);

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
