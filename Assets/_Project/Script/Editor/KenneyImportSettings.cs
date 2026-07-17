using UnityEditor;

// Kenney's RPG pack is smooth vector art, not pixel art: Point filtering would give it hard
// jaggies. Pixel-art sets elsewhere in the project still want Point - this only claims Art/Kenney.
public class KenneyImportSettings : AssetPostprocessor
{
    const string KenneyFolder = "Art/Kenney/";
    const string PiecesFolder = "Art/Terrain/Pieces/";
    const int PixelsPerUnit = 64;

    void OnPreprocessTexture()
    {
        var importer = (TextureImporter)assetImporter;

        // Generator source art must stay readable whatever else changes, so this one is not gated
        // on first import.
        if (assetPath.Contains(PiecesFolder))
        {
            importer.isReadable = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }

        if (!assetPath.Contains(KenneyFolder)) return;

        // Only seeds defaults on first import, so later hand-tuning in the Inspector sticks.
        if (!importer.importSettingsMissing) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.filterMode = UnityEngine.FilterMode.Bilinear;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = true;
    }
}
