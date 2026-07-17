using UnityEditor;

// Kenney's RPG pack is smooth vector art, not pixel art: Point filtering would give it hard
// jaggies. Pixel-art sets elsewhere in the project still want Point - this only claims Art/Kenney.
public class KenneyImportSettings : AssetPostprocessor
{
    const string Folder = "Art/Kenney/";
    const int PixelsPerUnit = 64;

    void OnPreprocessTexture()
    {
        if (!assetPath.Contains(Folder)) return;

        var importer = (TextureImporter)assetImporter;

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
