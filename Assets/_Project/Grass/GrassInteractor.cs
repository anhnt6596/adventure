using System.Collections.Generic;
using UnityEngine;

// A point that pushes grass aside - put on the player, enemies, anything that should part the grass.
[DisallowMultipleComponent]
public class GrassInteractor : MonoBehaviour
{
    [SerializeField] float radius = 0.6f;
    public float Radius => radius;

    void OnEnable() => GrassInteractorManager.Add(this);
    void OnDisable() => GrassInteractorManager.Remove(this);
}

// Uploads interactors to the grass shader once per frame. Auto-created. The shader loops over them
// per vertex, so this keeps only the Max nearest the camera - put the component on anything that
// moves through grass and the crowd is culled to what matters on screen.
class GrassInteractorManager : MonoBehaviour
{
    const int Max = 16;
    static readonly int InteractorsId = Shader.PropertyToID("_GrassInteractors");
    static readonly int CountId = Shader.PropertyToID("_GrassInteractorCount");

    static GrassInteractorManager _instance;
    readonly List<GrassInteractor> _list = new();
    readonly Vector4[] _packed = new Vector4[Max];
    readonly float[] _dist = new float[Max];

    static GrassInteractorManager Instance
    {
        get
        {
            if (_instance != null) return _instance;
            var go = new GameObject("[GrassInteractors]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<GrassInteractorManager>();
            return _instance;
        }
    }

    public static void Add(GrassInteractor i) => Instance._list.Add(i);
    public static void Remove(GrassInteractor i) { if (_instance != null) _instance._list.Remove(i); }

    void LateUpdate()
    {
        var cam = Camera.main;
        Vector3 eye = cam != null ? cam.transform.position : Vector3.zero;

        int n = 0;
        int farthest = 0;   // index into _dist of the current worst kept, valid once n == Max
        for (int i = _list.Count - 1; i >= 0; i--)
        {
            var g = _list[i];
            if (g == null) { _list.RemoveAt(i); continue; }

            var p = g.transform.position;
            var packed = new Vector4(p.x, p.y, p.z, g.Radius);
            float d = (p - eye).sqrMagnitude;

            if (n < Max)
            {
                _packed[n] = packed;
                _dist[n] = d;
                if (d > _dist[farthest]) farthest = n;
                n++;
            }
            else if (d < _dist[farthest])
            {
                _packed[farthest] = packed;
                _dist[farthest] = d;
                farthest = IndexOfMax(_dist, Max);
            }
        }

        Shader.SetGlobalVectorArray(InteractorsId, _packed);
        Shader.SetGlobalFloat(CountId, n);
    }

    static int IndexOfMax(float[] a, int len)
    {
        int m = 0;
        for (int i = 1; i < len; i++) if (a[i] > a[m]) m = i;
        return m;
    }
}
