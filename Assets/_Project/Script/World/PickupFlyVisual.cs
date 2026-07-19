using Lean.Pool;
using UnityEngine;

// A throwaway visual: spawned where a pickup was taken, it hops into the picker, shrinks, and despawns.
// Pure effect — the inventory is already credited, so this never touches logic. Put it on the ROOT of
// your fly prefab (root = Billboard, sprite is a child); it moves the root, the billboard keeps facing.
// Pooled via LeanPool, so it must restore its own scale on Launch (the last flight left it shrunk).
[DisallowMultipleComponent]
public class PickupFlyVisual : MonoBehaviour
{
    [SerializeField] float duration = 0.25f;
    [SerializeField] float arcMultiple = 2f;                 // arc peak = target height × this
    [SerializeField, Range(0f, 1f)] float endScale = 0.68f;  // shrinks toward this on the way in

    Transform _target;
    float _height;
    Vector3 _startPos, _startScale, _restScale;
    float _t;
    bool _launched;

    void Awake() => _restScale = transform.localScale;   // the true, un-shrunk scale; captured once

    public void Launch(Transform target, float height)
    {
        transform.localScale = _restScale;   // pooled reuse: undo the previous flight's shrink first
        _target = target;
        _height = height;
        _startPos = transform.position;
        _startScale = _restScale;
        _t = 0f;
        _launched = true;
    }

    void Update()
    {
        if (!_launched) return;

        _t += Time.deltaTime / Mathf.Max(0.0001f, duration);
        float k = Mathf.Clamp01(_t);

        // Live goal so it homes to a moving picker; a parabola on top makes it a jump, back to 0 on arrival.
        Vector3 goal = _target != null ? _target.position + Vector3.up * _height : _startPos;
        Vector3 pos = Vector3.Lerp(_startPos, goal, k);
        pos.y += (_height * arcMultiple) * 4f * k * (1f - k);
        transform.position = pos;
        transform.localScale = Vector3.Lerp(_startScale, _startScale * endScale, k);

        if (k >= 1f) LeanPool.Despawn(gameObject);
    }
}
