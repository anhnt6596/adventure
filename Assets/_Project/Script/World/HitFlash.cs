using UnityEngine;

// Flashes the object's sprites toward a colour on hit, then fades back. Drives the Sprite/Flash shader's
// _FlashAmount via a MaterialPropertyBlock (no material instances), so it can flash to WHITE — a plain
// SpriteRenderer.color multiply can't brighten to white. Pure view: listens to Damageable.Damaged.
// Needs the sprites to use a material with the "Sprite/Flash" shader; otherwise the writes are ignored.
[DisallowMultipleComponent]
public class HitFlash : MonoBehaviour
{
    static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
    static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");

    [SerializeField] Color flashColor = Color.white;
    [SerializeField, Range(0f, 1f)] float strength = 0.7f;   // peak flash amount (1 = full flash colour)
    [SerializeField] float duration = 0.15f;
    [SerializeField] SpriteRenderer[] renderers;   // left empty = auto-filled from children

    Damageable _damageable;
    MaterialPropertyBlock _mpb;
    float _t;

    void Awake()
    {
        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<SpriteRenderer>(true);

        _mpb = new MaterialPropertyBlock();
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
        if (_t > 0f) SetAmount(_t / duration * strength);   // peaks at `strength`, fading to 0
        else Clear();                                       // done → drop the block so sprites batch again
    }

    void SetAmount(float amount)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(FlashColorId, flashColor);
            _mpb.SetFloat(FlashAmountId, amount);
            r.SetPropertyBlock(_mpb);
        }
    }

    void Clear()
    {
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].SetPropertyBlock(null);
    }
}
