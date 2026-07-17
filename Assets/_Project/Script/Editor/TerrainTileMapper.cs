using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Assigns a tileset's sprites to their mask slots by reading the art itself: a tile's centre says
// what terrain it is, and each edge either matches that centre (no transition) or doesn't (one).
// Comparing edges to the tile's own centre means no palette needs configuring - it works for any
// tileset drawn this way.
//
// Editor-only by design: this is authoring, not something the game should redo at runtime.
public class TerrainTileMapper : EditorWindow
{
    [SerializeField] TerrainSet set;
    [SerializeField] int layerIndex;
    [SerializeField] float tolerance = 0.25f;

    Vector2 _scroll;
    readonly Dictionary<int, Sprite> _found = new Dictionary<int, Sprite>();
    readonly List<string> _rejected = new List<string>();

    [MenuItem("Tools/Terrain/Tile Mapper")]
    static void Open() => GetWindow<TerrainTileMapper>("Tile Mapper");

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Select the sprites of ONE terrain in the Project window (e.g. every water tile), " +
            "then Scan. Each tile's slot is worked out from its own pixels.",
            MessageType.Info);

        set = (TerrainSet)EditorGUILayout.ObjectField("Terrain Set", set, typeof(TerrainSet), false);
        if (set == null || set.Count == 0) return;

        layerIndex = EditorGUILayout.Popup("Layer", layerIndex,
            set.layers.Select((l, i) => $"{i}: {(string.IsNullOrEmpty(l.name) ? "?" : l.name)}").ToArray());
        layerIndex = Mathf.Clamp(layerIndex, 0, set.Count - 1);

        tolerance = EditorGUILayout.Slider("Colour Tolerance", tolerance, 0.05f, 0.8f);

        var sprites = Selection.objects.OfType<Sprite>()
            .Concat(Selection.objects.OfType<Texture2D>()
                .SelectMany(t => AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(t)).OfType<Sprite>()))
            .Distinct().ToList();

        EditorGUILayout.LabelField($"Selected sprites: {sprites.Count}");

        using (new EditorGUI.DisabledScope(sprites.Count == 0))
            if (GUILayout.Button("Scan Selection", GUILayout.Height(26)))
                Scan(sprites);

        if (_found.Count == 0 && _rejected.Count == 0) return;

        EditorGUILayout.Space();
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        int missing = 0;
        for (int mask = 0; mask < SameGrid.MaskCount; mask++)
        {
            _found.TryGetValue(mask, out var sprite);
            if (sprite == null) missing++;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"{mask,2}  {Describe(mask)}", GUILayout.Width(150));
                EditorGUILayout.ObjectField(sprite, typeof(Sprite), false);
            }
        }

        EditorGUILayout.EndScrollView();

        if (missing > 0)
            EditorGUILayout.HelpBox(
                $"{16 - missing}/16 slots filled. A 3x3 tileset only draws blobs, so the strip and " +
                "lone-cell slots have no art - those cells fall back to the plain tile, and a " +
                "one-cell-wide river will look wrong.",
                MessageType.Warning);

        if (_rejected.Count > 0)
            EditorGUILayout.HelpBox(
                $"Ignored {_rejected.Count} (unreadable, or the same slot twice):\n" +
                string.Join(", ", _rejected.Take(8)),
                MessageType.None);

        using (new EditorGUI.DisabledScope(_found.Count == 0))
            if (GUILayout.Button($"Assign to '{set.layers[layerIndex].name}'", GUILayout.Height(26)))
                Assign();
    }

    static string Describe(int mask)
    {
        if (mask == 0) return "plain (interior)";
        if (mask == 15) return "lone cell";

        string s = "";
        if ((mask & SameGrid.OpenNorth) != 0) s += "N";
        if ((mask & SameGrid.OpenEast) != 0) s += "E";
        if ((mask & SameGrid.OpenSouth) != 0) s += "S";
        if ((mask & SameGrid.OpenWest) != 0) s += "W";
        return $"edge {s}";
    }

    void Scan(List<Sprite> sprites)
    {
        _found.Clear();
        _rejected.Clear();

        foreach (var sprite in sprites)
        {
            if (!sprite.texture.isReadable)
            {
                _rejected.Add($"{sprite.name} (not readable)");
                continue;
            }

            int mask = MaskOf(sprite);
            if (mask < 0) { _rejected.Add($"{sprite.name} (unreadable pixels)"); continue; }

            // Kenney ships several interiors per terrain; the first wins and the rest are variants.
            if (_found.ContainsKey(mask)) { _rejected.Add($"{sprite.name} (slot {mask} taken)"); continue; }
            _found[mask] = sprite;
        }
    }

    int MaskOf(Sprite sprite)
    {
        var r = sprite.textureRect;
        int x0 = (int)r.xMin, y0 = (int)r.yMin, w = (int)r.width, h = (int)r.height;
        if (w < 8 || h < 8) return -1;

        Color centre = Average(sprite, x0 + w / 2 - 4, y0 + h / 2 - 4, 8, 8);

        // Samples the middle of each edge, so corner art never decides a side.
        int band = Mathf.Max(2, w / 8);
        int mid = w / 2 - band / 2;

        Color north = Average(sprite, x0 + mid, y0 + h - 2, band, 2);
        Color south = Average(sprite, x0 + mid, y0, band, 2);
        Color west = Average(sprite, x0, y0 + h / 2 - band / 2, 2, band);
        Color east = Average(sprite, x0 + w - 2, y0 + h / 2 - band / 2, 2, band);

        int mask = 0;
        if (Differs(north, centre)) mask |= SameGrid.OpenNorth;
        if (Differs(east, centre)) mask |= SameGrid.OpenEast;
        if (Differs(south, centre)) mask |= SameGrid.OpenSouth;
        if (Differs(west, centre)) mask |= SameGrid.OpenWest;
        return mask;
    }

    bool Differs(Color a, Color b)
        => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) > tolerance;

    static Color Average(Sprite sprite, int x, int y, int w, int h)
    {
        var pixels = sprite.texture.GetPixels(x, y, w, h);
        float r = 0, g = 0, b = 0;
        foreach (var p in pixels) { r += p.r; g += p.g; b += p.b; }
        int n = Mathf.Max(1, pixels.Length);
        return new Color(r / n, g / n, b / n);
    }

    void Assign()
    {
        Undo.RecordObject(set, "Assign Terrain Tiles");

        var layer = set.layers[layerIndex];
        if (layer.tiles == null || layer.tiles.Length != SameGrid.MaskCount)
            layer.tiles = new Sprite[SameGrid.MaskCount];

        foreach (var kv in _found) layer.tiles[kv.Key] = kv.Value;

        // Slots a 3x3 set never drew: better a seam than a hole in the ground.
        if (layer.tiles[0] != null)
            for (int mask = 0; mask < SameGrid.MaskCount; mask++)
                if (layer.tiles[mask] == null) layer.tiles[mask] = layer.tiles[0];

        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Terrain] Assigned {_found.Count} tiles to layer '{layer.name}'.");
    }
}
