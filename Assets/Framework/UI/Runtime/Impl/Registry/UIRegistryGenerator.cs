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
        public static void Regenerate(UIRegistry registry)
        {
            if (registry == null)
            {
                Debug.LogError("[UIRegistry] No registry assigned — assign the UI Registry asset to UISystem._registry, or no UXML will resolve.");
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
            AssetDatabase.SaveAssets();
            Debug.Log($"[UIRegistry] Regenerated: {registry.entries.Count} entries");
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
