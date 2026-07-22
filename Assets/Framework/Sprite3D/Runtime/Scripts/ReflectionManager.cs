using System.Collections.Generic;
using UnityEngine;

// One LateUpdate for ALL water reflections, instead of a WaterReflection.LateUpdate per caster. Auto-created
// on first Register — no scene setup. Unlike ShadowManager there's nothing to skip: a reflection follows a
// moving, animating caster, so every registered one ticks every frame. Reuses CameraViewDir.CamForward (the
// same facing the billboards use) so the reflection faces the camera exactly like the art it mirrors.
public class ReflectionManager : MonoBehaviour
{
    static ReflectionManager _instance;
    readonly List<WaterReflection> _reflections = new();

    static ReflectionManager Instance
    {
        get
        {
            if (_instance != null) return _instance;
            var go = new GameObject("[ReflectionManager]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ReflectionManager>();
            return _instance;
        }
    }

    public static void Register(WaterReflection r) => Instance._reflections.Add(r);

    public static void Unregister(WaterReflection r)
    {
        if (_instance != null) _instance._reflections.Remove(r);
    }

    void LateUpdate()
    {
        var f = CameraViewDir.CamForward;
        if (f.sqrMagnitude < 1e-6f) return;   // no CameraViewDir alive yet -> nothing valid to face

        var list = _reflections;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var r = list[i];
            if (r) r.Tick(f);
            else list.RemoveAt(i);   // cleanup any destroyed without OnDisable
        }
    }
}
