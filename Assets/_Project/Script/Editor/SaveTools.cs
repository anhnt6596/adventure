using System.IO;
using UnityEditor;
using UnityEngine;

// Dev tool: wipe the save files so a test starts fresh. SaveService writes one JSON per key into
// persistentDataPath, so clearing *.json there resets everything (inventories, currency, ...).
public static class SaveTools
{
    [MenuItem("Tools/Clear Save")]
    static void ClearSave()
    {
        string dir = Application.persistentDataPath;
        string[] files = Directory.Exists(dir) ? Directory.GetFiles(dir, "*.json") : new string[0];

        if (files.Length == 0)
        {
            Debug.Log($"[Clear Save] nothing to clear in {dir}");
            return;
        }

        if (!EditorUtility.DisplayDialog("Clear Save",
                $"Delete {files.Length} save file(s) in:\n{dir}?", "Delete", "Cancel"))
            return;

        foreach (var f in files) File.Delete(f);
        Debug.Log($"[Clear Save] deleted {files.Length} file(s) from {dir}");
    }
}
