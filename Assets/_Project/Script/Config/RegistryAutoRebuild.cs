#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Callbacks;
using Core.UI;

// The single place that keeps all three asset registries in sync in the editor, so the Rebuild/Regenerate
// menus stay a manual fallback. Each rebuilds only on changes it cares about; a registry asset is never a
// Config / prefab / uxml, so its SetDirty -> save can't re-trigger an import loop.
class RegistryAutoRebuild : AssetPostprocessor
{
    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        bool config = false, prefab = false, ui = false;

        void Scan(string[] paths, bool canLoad)
        {
            foreach (var p in paths)
            {
                if (p.EndsWith(".prefab")) prefab = true;
                else if (p.EndsWith(".uxml")) ui = true;
                else if (p.EndsWith(".asset"))
                    // a live Config counts; a deleted asset can't be loaded, so assume relevant
                    config |= !canLoad || AssetDatabase.LoadAssetAtPath<Config>(p) != null;
            }
        }

        Scan(imported, true);
        Scan(moved, true);
        Scan(deleted, false);

        if (config) ConfigRegistryBuilder.Rebuild(false);
        if (prefab) PrefabRegistryBuilder.Rebuild(false);
        if (ui) UIRegistryGenerator.Regenerate(false);
    }

    // A script change can retype a UI view, flip a class into/out of Config, or add Identifiable to a prefab
    // root — none of which is an asset import — so re-sync all three after each compile to keep them current.
    [DidReloadScripts]
    static void OnScriptsReloaded()
    {
        ConfigRegistryBuilder.Rebuild(false);
        PrefabRegistryBuilder.Rebuild(false);
        UIRegistryGenerator.Regenerate(false);
    }
}
#endif
