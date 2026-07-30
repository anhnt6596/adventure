using UnityEngine;

// Feeds the border-fog veil (Unlit/BorderFog) the one thing it cannot know for itself: where the current map's
// border actually is. Everything about how the fog LOOKS lives on the material — tune it in play mode and the
// numbers survive, which is the whole reason the split runs this way round rather than mirroring the material's
// values into inspector fields here.
//
// The rect is described as origin + two unit axes + size rather than min/max, so a map rotated about Y works
// without a special case. It costs two dot products in the shader, the same as an axis-aligned test would.
//
// Cell size is NOT authored: the band width is stated in cells because that is how the map is authored and where
// gates get placed, and the cell size is measured off the grid — one number, one owner.
[RequireComponent(typeof(MeshRenderer))]
public class MapBorderFog : MonoBehaviour
{
    // Which map to ring. Set by MapService when it wires a freshly loaded map into the scene: the same place, and
    // for the same reason, as CollisionSystem.SetTerrain — this system never learns how maps are loaded.
    public static TerrainGrid Terrain { get; set; }

    static readonly int RectId = Shader.PropertyToID("_MapRect");
    static readonly int AxisId = Shader.PropertyToID("_MapAxis");
    static readonly int GroundId = Shader.PropertyToID("_GroundY");
    static readonly int CellId = Shader.PropertyToID("_CellSize");

    MeshRenderer _renderer;
    MaterialPropertyBlock _block;

    void Awake()
    {
        _renderer = GetComponent<MeshRenderer>();
        _block = new MaterialPropertyBlock();

        // The renderer is authored DISABLED and only this script ever turns it on. Without a rect the shader has
        // nothing to measure against and blacks out the whole screen, so "no map yet" must mean "draw nothing" —
        // in edit mode too, where nothing sets the rect at all.
        _renderer.enabled = false;
    }

    // LateUpdate, and unconditionally: MaskFollowCamera has already placed the quad by now, and re-deriving the
    // rect is a couple of dozen float ops with no allocation — cheaper than caching it, and it keeps working if a
    // map is moved or resized while the game runs.
    void LateUpdate()
    {
        var grid = Terrain;
        _renderer.enabled = grid != null;
        if (grid == null) return;

        var tf = grid.transform;
        Vector3 origin = tf.TransformPoint(Vector3.zero);

        // TransformVector, not transform.right/forward: it carries the map's scale, so the size and the axes come
        // out of the same measurement instead of one of them silently ignoring a scaled map.
        Vector3 alongX = tf.TransformVector(new Vector3(grid.Width * grid.CellSize, 0f, 0f));
        Vector3 alongZ = tf.TransformVector(new Vector3(0f, 0f, grid.Height * grid.CellSize));

        var flatX = new Vector2(alongX.x, alongX.z);
        var flatZ = new Vector2(alongZ.x, alongZ.z);
        float sizeX = flatX.magnitude;
        float sizeZ = flatZ.magnitude;
        if (sizeX < 1e-4f || sizeZ < 1e-4f) { _renderer.enabled = false; return; }

        flatX /= sizeX;
        flatZ /= sizeZ;

        _block.SetVector(RectId, new Vector4(origin.x, origin.z, sizeX, sizeZ));
        _block.SetVector(AxisId, new Vector4(flatX.x, flatX.y, flatZ.x, flatZ.y));
        _block.SetFloat(GroundId, origin.y);
        _block.SetFloat(CellId, sizeX / Mathf.Max(grid.Width, 1));
        _renderer.SetPropertyBlock(_block);
    }
}
