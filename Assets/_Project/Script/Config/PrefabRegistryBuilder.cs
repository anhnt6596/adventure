#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

// Mirror of ConfigRegistryBuilder: gathers every prefab whose ROOT carries an Identifiable and bakes them
// into the single PrefabRegistry asset.
public static class PrefabRegistryBuilder
{
    [MenuItem("Tools/Config/Rebuild Prefab Registry")]
    public static void Rebuild() => Rebuild(true);

    // save=false: SetDirty only (for the auto-rebuild postprocessor) — see ConfigRegistryBuilder.
    public static void Rebuild(bool save)
    {
        var registry = AssetDatabase.FindAssets("t:PrefabRegistry")
            .Select(g => AssetDatabase.LoadAssetAtPath<PrefabRegistry>(AssetDatabase.GUIDToAssetPath(g)))
            .FirstOrDefault();

        if (registry == null)
        {
            if (save) Debug.LogWarning("[Prefab] No PrefabRegistry asset. Create one via Create > Config > Prefab Registry.");
            return;
        }

        var prefabs = AssetDatabase.FindAssets("t:Prefab")
            .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(go => go != null)
            .Select(go => go.GetComponent<Identifiable>())   // root only — the prefab's own identity
            .Where(c => c != null)
            .ToList();

        registry.SetAll(prefabs);
        EditorUtility.SetDirty(registry);
        if (save)
        {
            AssetDatabase.SaveAssets();
            Debug.Log($"[Prefab] Registry rebuilt: {prefabs.Count} prefabs.");
        }
    }
}
#endif
