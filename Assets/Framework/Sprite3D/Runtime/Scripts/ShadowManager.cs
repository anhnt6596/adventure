using System.Collections.Generic;
using UnityEngine;

// One LateUpdate for ALL ground shadows, instead of a SpriteShadow.LateUpdate per caster (a forest of
// trees is hundreds of native calls). Auto-created on first Register — no scene setup.
//
// The heavy part — a shadow's yaw to the sun — is identical for every shadow and only changes as the day
// moves, so it's computed once here and pushed only on the frames it actually changed. Per-shadow work is
// then just a sprite ref-check (for animation) and the cheap flip test.
public class ShadowManager : MonoBehaviour
{
    static ShadowManager _instance;
    readonly List<SpriteShadow> _shadows = new();
    bool _hasNew;
    float _lastSunYaw = float.NaN;
    Camera _cam;

    static readonly int SunDirId = Shader.PropertyToID("_SunGroundDir");

    static ShadowManager Instance
    {
        get
        {
            if (_instance != null) return _instance;
            var go = new GameObject("[ShadowManager]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ShadowManager>();
            return _instance;
        }
    }

    public static void Register(SpriteShadow s)
    {
        var m = Instance;
        m._shadows.Add(s);
        m._hasNew = true;   // orient it next LateUpdate even if the sun is still
    }

    public static void Unregister(SpriteShadow s)
    {
        if (_instance != null) _instance._shadows.Remove(s);
    }

    void LateUpdate()
    {
        Vector4 sun = Shader.GetGlobalVector(SunDirId);   // .xy = world (X,Z) the shadows point
        float sunYaw = Mathf.Atan2(sun.x, sun.y) * Mathf.Rad2Deg;
        bool orient = _hasNew || !Mathf.Approximately(sunYaw, _lastSunYaw);   // skip the yaw write on still frames
        _lastSunYaw = sunYaw;
        _hasNew = false;

        if (_cam == null) _cam = Camera.main;
        Vector3 camRight = _cam != null ? _cam.transform.right : Vector3.right;

        var list = _shadows;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var s = list[i];
            if (s) s.Tick(sunYaw, orient, sun, camRight);
            else list.RemoveAt(i);   // cleanup any destroyed without OnDisable
        }
    }
}
