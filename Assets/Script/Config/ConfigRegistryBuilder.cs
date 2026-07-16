#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ConfigRegistryBuilder
{
    [MenuItem("Tools/Config/Rebuild Registry")]
    public static void Rebuild()
    {
        var registry = AssetDatabase.FindAssets("t:ConfigRegistry")
            .Select(g => AssetDatabase.LoadAssetAtPath<ConfigRegistry>(AssetDatabase.GUIDToAssetPath(g)))
            .FirstOrDefault();

        if (registry == null)
        {
            Debug.LogWarning("[Config] No ConfigRegistry asset. Create one via Create > Config > Registry.");
            return;
        }

        var configs = AssetDatabase.FindAssets("t:Config")
            .Select(g => AssetDatabase.LoadAssetAtPath<Config>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(c => c != null)
            .ToList();

        registry.SetAll(configs);
        EditorUtility.SetDirty(registry);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Config] Registry rebuilt: {configs.Count} configs.");
    }
}
#endif
