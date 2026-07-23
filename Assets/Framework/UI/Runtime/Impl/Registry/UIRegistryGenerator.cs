#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Core.UI
{
    public static class UIRegistryGenerator
    {
        [MenuItem("Tools/UI/Regenerate UI Registry")]
        public static void Regenerate() => Regenerate(true);

        // save=false: SetDirty only (no SaveAssets / log) — for the auto-rebuild postprocessor. Finds the single
        // UIRegistry asset itself, like the ConfigRegistry / PrefabRegistry builders.
        public static void Regenerate(bool save)
        {
            var registry = AssetDatabase.FindAssets("t:UIRegistry")
                .Select(g => AssetDatabase.LoadAssetAtPath<UIRegistry>(AssetDatabase.GUIDToAssetPath(g)))
                .FirstOrDefault();

            if (registry == null)
            {
                if (save) Debug.LogError("[UIRegistry] No UIRegistry asset — create one via Create > UI > UI Registry, and assign it to UISystem.");
                return;
            }

            var guids = AssetDatabase.FindAssets("t:VisualTreeAsset", new[] { "Assets" });

            registry.entries.Clear();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var vta = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
                if (vta == null) continue;

                var fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                var type = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(GetLoadableTypes)
                    .FirstOrDefault(t => t.Name == fileName && typeof(IUIElement).IsAssignableFrom(t));

                if (type == null) continue;

                registry.entries.Add(new UIRegistry.Entry
                {
                    viewTypeName = type.AssemblyQualifiedName,
                    asset = vta
                });
            }

            EditorUtility.SetDirty(registry);
            if (save)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[UIRegistry] Regenerated: {registry.entries.Count} entries");
            }
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null);
            }
        }
    }
}
#endif
