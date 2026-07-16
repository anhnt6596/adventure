#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Press Play in ANY scene -> boots from LoadingScene (so App/DI always exist).
// Exiting play mode returns to the scene you were editing.
// Toggle: Tools > Play From Loading.
[InitializeOnLoad]
public static class PlayFromLoading
{
    const string LoadingScenePath = "Assets/_Project/Scenes/LoadingScene.unity";
    const string MenuPath = "Tools/Play From Loading";
    const string PrefKey = "PlayFromLoading.Enabled";

    static bool Enabled
    {
        get => EditorPrefs.GetBool(PrefKey, true);
        set => EditorPrefs.SetBool(PrefKey, value);
    }

    static PlayFromLoading() => EditorApplication.delayCall += Apply;

    [MenuItem(MenuPath)]
    static void Toggle()
    {
        Enabled = !Enabled;
        Apply();
        Debug.Log($"[PlayFromLoading] {(Enabled ? "ON — Play always boots LoadingScene" : "OFF — Play starts in the open scene")}");
    }

    [MenuItem(MenuPath, isValidateFunction: true)]
    static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, Enabled);
        return true;
    }

    static void Apply()
    {
        if (!Enabled) { EditorSceneManager.playModeStartScene = null; return; }

        var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(LoadingScenePath);
        if (scene == null)
        {
            Debug.LogWarning($"[PlayFromLoading] LoadingScene not found at {LoadingScenePath}");
            return;
        }
        EditorSceneManager.playModeStartScene = scene;
    }
}
#endif
