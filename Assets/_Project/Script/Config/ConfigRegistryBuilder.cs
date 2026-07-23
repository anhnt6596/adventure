#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ConfigRegistryBuilder
{
    [MenuItem("Tools/Config/Rebuild Registry")]
    public static void Rebuild() => Rebuild(true);

    // save=false: only SetDirty (no SaveAssets, no log) — for the auto-rebuild postprocessor, so it doesn't
    // write assets mid-import (which would loop) or spam the console. The in-memory registry is still current.
    public static void Rebuild(bool save)
    {
        var registry = AssetDatabase.FindAssets("t:ConfigRegistry")
            .Select(g => AssetDatabase.LoadAssetAtPath<ConfigRegistry>(AssetDatabase.GUIDToAssetPath(g)))
            .FirstOrDefault();

        if (registry == null)
        {
            if (save) Debug.LogWarning("[Config] No ConfigRegistry asset. Create one via Create > Config > Registry.");
            return;
        }

        var configs = AssetDatabase.FindAssets("t:Config")
            .Select(g => AssetDatabase.LoadAssetAtPath<Config>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(c => c != null)
            .ToList();

        registry.SetAll(configs);
        EditorUtility.SetDirty(registry);
        if (save)
        {
            AssetDatabase.SaveAssets();
            Debug.Log($"[Config] Registry rebuilt: {configs.Count} configs.");
        }
    }
}
#endif
