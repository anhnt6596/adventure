using System.Collections.Generic;
using UnityEngine;

// One LateUpdate for ALL billboards instead of N MonoBehaviour.LateUpdate calls.
// Auto-created on first Register — no scene setup needed.
// Requires a CameraViewDir component on the camera (it feeds CamForward/TransformChanged).
public class BillboardManager : MonoBehaviour
{
    static BillboardManager _instance;
    readonly List<Transform> _billboards = new();
    bool _hasNew;
    float _warnAt;

    static BillboardManager Instance
    {
        get
        {
            if (_instance != null) return _instance;
            var go = new GameObject("[BillboardManager]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<BillboardManager>();
            return _instance;
        }
    }

    public static void Register(Transform t)
    {
        var m = Instance;
        m._billboards.Add(t);
        m._hasNew = true;            // orient it next LateUpdate even if the camera never moves
    }

    public static void Unregister(Transform t)
    {
        if (_instance != null) _instance._billboards.Remove(t);
    }

    void LateUpdate()
    {
        var f = CameraViewDir.CamForward;

        if (f.sqrMagnitude < 1e-6f)   // no CameraViewDir alive yet -> nothing valid to face
        {
            if (_billboards.Count > 0 && Time.unscaledTime > _warnAt)
            {
                _warnAt = Time.unscaledTime + 5f;
                Debug.LogWarning("[BillboardManager] No CameraViewDir feeding CamForward — add the CameraViewDir component to your camera, or billboards won't rotate.");
            }
            return;
        }

        if (!CameraViewDir.TransformChanged && !_hasNew) return;   // still camera -> O(1)
        _hasNew = false;

        var list = _billboards;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var t = list[i];
            if (t) t.forward = f;
            else list.RemoveAt(i);    // cleanup any destroyed without OnDisable
        }
    }
}
