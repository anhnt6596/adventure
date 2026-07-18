using UnityEngine;

// Flashes the object's sprites toward a colour on hit, then fades back over `duration`. Pure view:
// listens to Damageable.Damaged, knows nothing about HP. Sits on the same GameObject as Damageable.
[DisallowMultipleComponent]
public class HitFlash : MonoBehaviour
{
    [SerializeField] Color flashColor = Color.red;
    [SerializeField] float duration = 0.15f;
    [SerializeField] SpriteRenderer[] renderers;   // left empty = auto-filled from children

    Damageable _damageable;
    Color[] _base;
    float _t;

    void Awake()
    {
        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<SpriteRenderer>(true);

        _base = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            _base[i] = renderers[i].color;

        _damageable = GetComponent<Damageable>();
        if (_damageable == null)
            Debug.LogError($"[{nameof(HitFlash)}] no Damageable on this object — nothing to flash on.", this);
    }

    void OnEnable()  { if (_damageable != null) _damageable.Damaged += Flash; }
    void OnDisable() { if (_damageable != null) _damageable.Damaged -= Flash; }

    void Flash() => _t = duration;

    void Update()
    {
        if (_t <= 0f) return;

        _t -= Time.deltaTime;
        float k = Mathf.Clamp01(_t / duration);   // 1 = full flash colour, 0 = back to base
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null)
                renderers[i].color = Color.Lerp(_base[i], flashColor, k);
    }
}
