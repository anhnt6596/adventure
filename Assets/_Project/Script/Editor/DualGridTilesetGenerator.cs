using System.IO;
using UnityEditor;
using UnityEngine;

// Builds the 16 dual-grid tiles from three quadrant pieces. Every tile is four quadrants, and a
// quadrant only ever has four appearances, so three drawings plus rotation cover the whole set.
//
// Output slot index IS the mask, so tiles[mask] maps straight onto the sliced sprite _mask.
public class DualGridTilesetGenerator : EditorWindow
{
    enum Quadrant { SW, SE, NE, NW }

    [SerializeField] Texture2D solid;
    [SerializeField] Texture2D edge;
    [SerializeField] Texture2D outerCorner;
    [SerializeField] Texture2D innerCorner;
    [SerializeField] string outputPath = "Assets/_Project/Art/Terrain/Generated.png";

    [MenuItem("Tools/Terrain/Dual Grid Tileset Generator")]
    static void Open() => GetWindow<DualGridTilesetGenerator>("Dual Grid Tileset");

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Draw three quadrant pieces in SW orientation (the piece as it looks in the tile's " +
            "bottom-left corner). They are rotated to fill the other three.\n\n" +
            "Pieces must be square, the same size, and Read/Write enabled.",
            MessageType.Info);

        EditorGUILayout.Space();
        solid = (Texture2D)EditorGUILayout.ObjectField("Solid", solid, typeof(Texture2D), false);
        edge = (Texture2D)EditorGUILayout.ObjectField("Edge (rim on top)", edge, typeof(Texture2D), false);
        outerCorner = (Texture2D)EditorGUILayout.ObjectField("Outer Corner", outerCorner, typeof(Texture2D), false);
        innerCorner = (Texture2D)EditorGUILayout.ObjectField("Inner Corner", innerCorner, typeof(Texture2D), false);

        EditorGUILayout.Space();
        outputPath = EditorGUILayout.TextField("Output", outputPath);

        EditorGUILayout.Space();
        string error = Validate();
        if (error != null)
        {
            EditorGUILayout.HelpBox(error, MessageType.Warning);
            return;
        }

        int q = solid.width;
        EditorGUILayout.LabelField($"Tile size: {q * 2}px  ->  atlas {q * 8}x{q * 8} (4x4)");

        if (GUILayout.Button("Generate", GUILayout.Height(30))) Generate();
    }

    string Validate()
    {
        if (solid == null || edge == null || outerCorner == null || innerCorner == null)
            return "Assign all four pieces.";

        foreach (var t in new[] { solid, edge, outerCorner, innerCorner })
        {
            if (!t.isReadable)
                return $"'{t.name}' is not readable. Enable Read/Write in its import settings.";
            if (t.width != t.height)
                return $"'{t.name}' is not square ({t.width}x{t.height}).";
            if (t.width != solid.width)
                return "All three pieces must be the same size.";
        }
        return null;
    }

    void Generate()
    {
        int q = solid.width;
        int tile = q * 2;

        var atlas = new Texture2D(tile * 4, tile * 4, TextureFormat.RGBA32, false);
        var blank = new Color[atlas.width * atlas.height];
        atlas.SetPixels(blank);

        for (int mask = 0; mask < DualGrid.MaskCount; mask++)
        {
            int col = mask % 4;
            int row = mask / 4;

            // Unity slices left-to-right, top-to-bottom, but texture space starts bottom-left.
            int ox = col * tile;
            int oy = atlas.height - (row + 1) * tile;

            DrawQuadrant(atlas, mask, Quadrant.SW, ox, oy);
            DrawQuadrant(atlas, mask, Quadrant.SE, ox + q, oy);
            DrawQuadrant(atlas, mask, Quadrant.NW, ox, oy + q);
            DrawQuadrant(atlas, mask, Quadrant.NE, ox + q, oy + q);
        }

        atlas.Apply();
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
        File.WriteAllBytes(outputPath, atlas.EncodeToPNG());
        DestroyImmediate(atlas);

        AssetDatabase.ImportAsset(outputPath);
        ApplyImportSettings(outputPath, tile);

        Debug.Log($"[DualGrid] Generated 16 tiles at {tile}px -> {outputPath}");
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<Texture2D>(outputPath);
    }

    void DrawQuadrant(Texture2D atlas, int mask, Quadrant quadrant, int x, int y)
    {
        GetBits(quadrant, out int selfBit, out int hBit, out int vBit, out int dBit);

        bool self = (mask & selfBit) != 0;
        bool h = (mask & hBit) != 0;
        bool v = (mask & vBit) != 0;
        bool d = (mask & dBit) != 0;

        Texture2D piece;
        bool mirror = false;

        if (self)
        {
            if (!h && !v) piece = outerCorner;
            else if (h && v) piece = solid;
            else { piece = edge; mirror = !h; }  // only the v-side is open -> the rim turns
        }
        else if (h && v && d)
        {
            piece = innerCorner;
        }
        else
        {
            return; // empty: the layer below shows through
        }

        Blit(atlas, piece, x, y, RotationSteps(quadrant), mirror);
    }

    // Roles follow the CCW rotation that places the SW-authored piece: rotating SW's east neighbour
    // lands on SE's north, so SE reads h from NE - not from SW. Getting this backwards is invisible
    // until a piece distinguishes h from v, which `edge` does.
    static void GetBits(Quadrant q, out int self, out int h, out int v, out int d)
    {
        switch (q)
        {
            case Quadrant.SW: self = DualGrid.BitSouthWest; h = DualGrid.BitSouthEast; v = DualGrid.BitNorthWest; d = DualGrid.BitNorthEast; break;
            case Quadrant.SE: self = DualGrid.BitSouthEast; h = DualGrid.BitNorthEast; v = DualGrid.BitSouthWest; d = DualGrid.BitNorthWest; break;
            case Quadrant.NE: self = DualGrid.BitNorthEast; h = DualGrid.BitNorthWest; v = DualGrid.BitSouthEast; d = DualGrid.BitSouthWest; break;
            default: self = DualGrid.BitNorthWest; h = DualGrid.BitSouthWest; v = DualGrid.BitNorthEast; d = DualGrid.BitSouthEast; break;
        }
    }

    // Pieces are authored for SW; 90 degrees CCW walks SW -> SE -> NE -> NW.
    static int RotationSteps(Quadrant q) => q switch
    {
        Quadrant.SW => 0,
        Quadrant.SE => 1,
        Quadrant.NE => 2,
        _ => 3,
    };

    static void Blit(Texture2D dst, Texture2D src, int x, int y, int rotations, bool mirror)
    {
        int q = src.width;
        var pixels = src.GetPixels();

        // Mirroring before rotating turns the top rim into a side rim, so one edge drawing covers
        // both open sides.
        if (mirror) pixels = Transpose(pixels, q);
        for (int i = 0; i < rotations; i++) pixels = Rotate90CCW(pixels, q);

        dst.SetPixels(x, y, q, q, pixels);
    }

    static Color[] Transpose(Color[] src, int q)
    {
        var dst = new Color[src.Length];
        for (int y = 0; y < q; y++)
            for (int x = 0; x < q; x++)
                dst[y * q + x] = src[x * q + y];
        return dst;
    }

    static Color[] Rotate90CCW(Color[] src, int q)
    {
        var dst = new Color[src.Length];
        for (int y = 0; y < q; y++)
            for (int x = 0; x < q; x++)
                dst[y * q + x] = src[(q - 1 - x) * q + y];
        return dst;
    }

    static void ApplyImportSettings(string path, int tileSize)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.spritePixelsPerUnit = tileSize;

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        var rects = new UnityEditor.U2D.Sprites.SpriteRect[DualGrid.MaskCount];
        for (int mask = 0; mask < DualGrid.MaskCount; mask++)
        {
            int col = mask % 4;
            int row = mask / 4;
            rects[mask] = new UnityEditor.U2D.Sprites.SpriteRect
            {
                name = $"{Path.GetFileNameWithoutExtension(path)}_{mask}",
                spriteID = GUID.Generate(),
                rect = new Rect(col * tileSize, (3 - row) * tileSize, tileSize, tileSize),
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
            };
        }

        provider.SetSpriteRects(rects);
        provider.Apply();
        importer.SaveAndReimport();
    }
}
