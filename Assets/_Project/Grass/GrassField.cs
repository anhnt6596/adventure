using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Scatters billboard grass over a painted density mask and draws it instanced per visible chunk.
// The mask is a grid over the field; paint it in the scene view (see GrassFieldEditor). No
// GameObjects, no MeshRenderers.
[ExecuteAlways]
public class GrassField : MonoBehaviour
{
    [SerializeField] Material material;
    [SerializeField] Vector2 size = new Vector2(20f, 20f);
    [SerializeField] float density = 8f;              // instances per square unit where the mask is full
    [SerializeField] float chunkSize = 8f;            // world size of one cull chunk
    [SerializeField] float maskCellSize = 1f;         // world size of one paint cell
    [SerializeField] Vector2 heightRange = new Vector2(0.8f, 1.3f);
    [SerializeField] Vector2 widthRange = new Vector2(0.7f, 1.1f);
    [SerializeField] float sink = 0f;
    [SerializeField] int seed = 1;

    [SerializeField, HideInInspector] byte[] mask;    // 0..255 density per cell
    [SerializeField, HideInInspector] int maskCols, maskRows;

    struct Chunk { public Bounds bounds; public Matrix4x4[] matrices; }

    Mesh _quad;
    Chunk[] _chunks;
    RenderParams _rp;
    Vector3 _builtOrigin;
    readonly Plane[] _planes = new Plane[6];

    public Vector2 Size => size;
    public float MaskCellSize => maskCellSize;

    void OnEnable() => Rebuild();
    void OnValidate() { if (isActiveAndEnabled) Rebuild(); }

    void EnsureMask()
    {
        int cols = Mathf.Max(1, Mathf.CeilToInt(size.x / Mathf.Max(0.1f, maskCellSize)));
        int rows = Mathf.Max(1, Mathf.CeilToInt(size.y / Mathf.Max(0.1f, maskCellSize)));
        if (mask != null && maskCols == cols && maskRows == rows) return;

        var resized = new byte[cols * rows];
        if (mask != null)
            for (int y = 0; y < Mathf.Min(rows, maskRows); y++)
                for (int x = 0; x < Mathf.Min(cols, maskCols); x++)
                    resized[y * cols + x] = mask[y * maskCols + x];
        else
            for (int i = 0; i < resized.Length; i++) resized[i] = 255;   // start fully grassed

        mask = resized; maskCols = cols; maskRows = rows;
    }

    float MaskAt(float u, float v)   // u,v in 0..1 across the field
    {
        if (mask == null) return 1f;
        int x = Mathf.Clamp((int)(u * maskCols), 0, maskCols - 1);
        int y = Mathf.Clamp((int)(v * maskRows), 0, maskRows - 1);
        return mask[y * maskCols + x] / 255f;
    }

    void Rebuild()
    {
        if (_quad == null) _quad = BuildQuad();
        EnsureMask();

        int candidates = Mathf.Clamp(Mathf.RoundToInt(size.x * size.y * density), 0, 300000);
        Vector3 origin = transform.position;
        _builtOrigin = origin;

        int cols = Mathf.Max(1, Mathf.CeilToInt(size.x / Mathf.Max(0.1f, chunkSize)));
        int rows = Mathf.Max(1, Mathf.CeilToInt(size.y / Mathf.Max(0.1f, chunkSize)));
        var buckets = new List<Matrix4x4>[cols * rows];
        for (int i = 0; i < buckets.Length; i++) buckets[i] = new List<Matrix4x4>();

        var rng = new System.Random(seed);
        float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        for (int i = 0; i < candidates; i++)
        {
            float u = (float)rng.NextDouble();
            float v = (float)rng.NextDouble();
            if (rng.NextDouble() >= MaskAt(u, v)) continue;   // thinned by the painted density

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
                var center = origin + new Vector3(
                    (cx + 0.5f) / cols * size.x - size.x * 0.5f, 1f,
                    (cz + 0.5f) / rows * size.y - size.y * 0.5f);
                var ext = new Vector3(size.x / cols, 4f, size.y / rows);
                _chunks[idx] = new Chunk { bounds = new Bounds(center, ext), matrices = buckets[idx].ToArray() };
            }
    }

    void Update()
    {
        if (material == null) return;
        if (_chunks == null || transform.position != _builtOrigin) Rebuild();
        if (!material.enableInstancing) material.enableInstancing = true;

        _rp = new RenderParams(material)
        {
            worldBounds = new Bounds(transform.position, new Vector3(size.x, 4f, size.y)),
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false,
            layer = gameObject.layer,
        };

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
            Graphics.RenderMeshInstanced(_rp, _quad, 0, matrices, Mathf.Min(batch, matrices.Length - start), start);
    }

#if UNITY_EDITOR
    // Paints the density mask - called by GrassFieldEditor. value 255 = full grass, 0 = none.
    public bool Paint(Vector3 worldPos, float radius, byte value)
    {
        EnsureMask();
        Vector3 local = worldPos - (transform.position - new Vector3(size.x, 0, size.y) * 0.5f);
        int r = Mathf.Max(0, Mathf.CeilToInt(radius / maskCellSize) - 1);
        int cx = (int)(local.x / maskCellSize);
        int cy = (int)(local.z / maskCellSize);

        bool changed = false;
        for (int y = cy - r; y <= cy + r; y++)
            for (int x = cx - r; x <= cx + r; x++)
            {
                if ((uint)x >= (uint)maskCols || (uint)y >= (uint)maskRows) continue;
                if (mask[y * maskCols + x] == value) continue;
                mask[y * maskCols + x] = value;
                changed = true;
            }
        return changed;
    }

    public void RebuildFromEditor() => Rebuild();

    public void SetAllMask(byte value)
    {
        EnsureMask();
        for (int i = 0; i < mask.Length; i++) mask[i] = value;
        Rebuild();
    }
#endif

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

#if UNITY_EDITOR
    [SerializeField] bool drawMask = true;
#endif

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 1f, 0.4f, 0.4f);
        Gizmos.DrawWireCube(transform.position, new Vector3(size.x, 0.1f, size.y));

#if UNITY_EDITOR
        if (!drawMask || mask == null) return;
        EnsureMask();
        Vector3 min = transform.position - new Vector3(size.x, 0f, size.y) * 0.5f;
        var cell = new Vector3(maskCellSize * 0.92f, 0.02f, maskCellSize * 0.92f);
        for (int y = 0; y < maskRows; y++)
            for (int x = 0; x < maskCols; x++)
            {
                float d = mask[y * maskCols + x] / 255f;
                if (d <= 0f) continue;
                Gizmos.color = new Color(0.3f, 1f, 0.3f, 0.15f + d * 0.35f);
                Gizmos.DrawCube(min + new Vector3((x + 0.5f) * maskCellSize, 0.02f, (y + 0.5f) * maskCellSize), cell);
            }
#endif
    }
}
