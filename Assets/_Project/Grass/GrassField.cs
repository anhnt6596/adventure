using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Scatters billboard grass and draws it instanced, one call per visible chunk. The field is split into
// a grid of chunks; each frame only the chunks inside the camera frustum are drawn, so an off-screen
// field costs nothing. No GameObjects, no MeshRenderers.
[ExecuteAlways]
public class GrassField : MonoBehaviour
{
    [SerializeField] Material material;
    [SerializeField] Vector2 size = new Vector2(20f, 20f);
    [SerializeField] float density = 8f;              // instances per square unit
    [SerializeField] float chunkSize = 8f;            // world size of one cull chunk
    [SerializeField] Vector2 heightRange = new Vector2(0.8f, 1.3f);
    [SerializeField] Vector2 widthRange = new Vector2(0.7f, 1.1f);
    [Tooltip("Push the grass base down to meet the ground (sprite bottom padding, or field above ground).")]
    [SerializeField] float sink = 0f;
    [SerializeField] int seed = 1;

    struct Chunk { public Bounds bounds; public Matrix4x4[] matrices; }

    Mesh _quad;
    Chunk[] _chunks;
    RenderParams _rp;
    Vector3 _builtOrigin;
    readonly Plane[] _planes = new Plane[6];

    void OnEnable() => Rebuild();
    void OnValidate() { if (isActiveAndEnabled) Rebuild(); }

    void Rebuild()
    {
        if (_quad == null) _quad = BuildQuad();

        int total = Mathf.Clamp(Mathf.RoundToInt(size.x * size.y * density), 0, 300000);
        Vector3 origin = transform.position;
        _builtOrigin = origin;

        int cols = Mathf.Max(1, Mathf.CeilToInt(size.x / Mathf.Max(0.1f, chunkSize)));
        int rows = Mathf.Max(1, Mathf.CeilToInt(size.y / Mathf.Max(0.1f, chunkSize)));

        var buckets = new List<Matrix4x4>[cols * rows];
        for (int i = 0; i < buckets.Length; i++) buckets[i] = new List<Matrix4x4>();

        var rng = new System.Random(seed);
        float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        for (int i = 0; i < total; i++)
        {
            float u = (float)rng.NextDouble();   // 0..1 across X
            float v = (float)rng.NextDouble();   // 0..1 across Z
            var pos = origin + new Vector3((u - 0.5f) * size.x, -sink, (v - 0.5f) * size.y);
            var scale = new Vector3(Rand(widthRange.x, widthRange.y), Rand(heightRange.x, heightRange.y), 1f);

            int cx = Mathf.Min(cols - 1, (int)(u * cols));
            int cz = Mathf.Min(rows - 1, (int)(v * rows));
            buckets[cz * cols + cx].Add(Matrix4x4.TRS(pos, Quaternion.identity, scale));
        }

        _chunks = new Chunk[cols * rows];
        for (int cz = 0; cz < rows; cz++)
            for (int cx = 0; cx < cols; cx++)
            {
                int idx = cz * cols + cx;
                var m = buckets[idx].ToArray();
                var center = origin + new Vector3(
                    (cx + 0.5f) / cols * size.x - size.x * 0.5f, 1f,
                    (cz + 0.5f) / rows * size.y - size.y * 0.5f);
                var ext = new Vector3(size.x / cols, 4f, size.y / rows);
                _chunks[idx] = new Chunk { bounds = new Bounds(center, ext), matrices = m };
            }
    }

    void Update()
    {
        if (material == null) return;
        if (_chunks == null || transform.position != _builtOrigin) Rebuild();   // follow the object
        if (!material.enableInstancing) material.enableInstancing = true;

        _rp = new RenderParams(material)
        {
            worldBounds = new Bounds(transform.position, new Vector3(size.x, 4f, size.y)),
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false,
            layer = gameObject.layer,
        };

        // Cull by the game camera at runtime; in the editor draw everything so the Scene view is not
        // clipped to what the game camera happens to see.
        bool cull = Application.isPlaying && Camera.main != null;
        if (cull) GeometryUtility.CalculateFrustumPlanes(Camera.main, _planes);

        foreach (var chunk in _chunks)
        {
            if (chunk.matrices.Length == 0) continue;
            if (cull && !GeometryUtility.TestPlanesAABB(_planes, chunk.bounds)) continue;
            DrawBatched(chunk.matrices);
        }
    }

    void DrawBatched(Matrix4x4[] matrices)
    {
        const int batch = 1023;
        for (int start = 0; start < matrices.Length; start += batch)
        {
            int n = Mathf.Min(batch, matrices.Length - start);
            Graphics.RenderMeshInstanced(_rp, _quad, 0, matrices, n, start);
        }
    }

    static Mesh BuildQuad()
    {
        var m = new Mesh { name = "GrassQuad" };
        m.vertices = new[]
        {
            new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f),
            new Vector3(-0.5f, 1f, 0f), new Vector3(0.5f, 1f, 0f),
        };
        m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
        m.triangles = new[] { 0, 2, 1, 1, 2, 3 };
        m.RecalculateBounds();
        return m;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 1f, 0.4f, 0.4f);
        Gizmos.DrawWireCube(transform.position, new Vector3(size.x, 0.1f, size.y));
        if (_chunks == null) return;
        Gizmos.color = new Color(0.4f, 1f, 0.4f, 0.15f);
        foreach (var c in _chunks) Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
    }
}
