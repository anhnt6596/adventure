using System.Collections.Generic;
using UnityEngine;

// One LateUpdate for ALL billboards instead of N MonoBehaviour.LateUpdate calls.
// Auto-created on first Register — no scene setup needed.
// When the camera is still (dirty flag false) this costs O(1) per frame.
public class BillboardManager : MonoBehaviour
{
    static BillboardManager _instance;
    readonly List<Transform> _billboards = new();

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

    public static void Register(Transform t) => Instance._billboards.Add(t);

    public static void Unregister(Transform t)
    {
        if (_instance != null) _instance._billboards.Remove(t);
    }

    void LateUpdate()
    {
        if (!CameraViewDir.TransformChanged) return;   // camera still -> nothing to do
        var f = CameraViewDir.CamForward;              // set f.y = 0 here if you want upright sprites
        var list = _billboards;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var t = list[i];
            if (t) t.forward = f;
            else list.RemoveAt(i);                      // cleanup any that were destroyed without OnDisable
        }
    }
}
