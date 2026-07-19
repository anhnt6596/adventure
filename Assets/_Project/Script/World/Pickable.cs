using System.Collections.Generic;
using UnityEngine;

// A thing a Picker can grab when in range. Registers itself so pickers find it without scanning the
// scene. While a piece is still flying out (FlyingPickup) it's not pickable; it turns pickable on
// landing. When picked it hops into the picker in an arc, shrinking as it goes, then finishes.
[DisallowMultipleComponent]
public class Pickable : MonoBehaviour
{
    // Simple registry — a Picker iterates this instead of scanning the scene. Can graduate to a
    // DI-registered service (like CombatWorld) if pickups ever need spatial queries at scale.
    public static readonly List<Pickable> Active = new List<Pickable>();

    [Header("Collect — hop into the picker")]
    [SerializeField] float collectDuration = 0.25f;
    [SerializeField] float collectArcMultiple = 2f;                 // arc peak = pickHeight × this
    [SerializeField, Range(0f, 1f)] float collectEndScale = 0.68f;   // shrinks to ~half on the way in

    public bool CanPick { get; private set; }
    public Vector3 Position => transform.position;
    public event System.Action<Pickable> Picked;

    Transform _target;
    float _height;
    Vector3 _startPos, _startScale;
    float _t;
    bool _collecting;

    void OnEnable()
    {
        CanPick = true;
        Active.Add(this);
    }

    void OnDisable() => Active.Remove(this);

    public void SetPickable(bool value) => CanPick = value;

    // Start hopping into the picker; finishes (event + destroy) on arrival. `target` is tracked live so it
    // homes even if the picker moves; `height` lifts the goal off the ground (the picker's Y is 0).
    public void CollectTo(Transform target, float height)
    {
        if (!CanPick || _collecting) return;
        CanPick = false;          // out of the pick pool right away
        _collecting = true;
        _target = target;
        _height = height;
        _startPos = transform.position;
        _startScale = transform.localScale;
        _t = 0f;
    }

    void Update()
    {
        if (!_collecting) return;

        _t += Time.deltaTime / Mathf.Max(0.0001f, collectDuration);
        float k = Mathf.Clamp01(_t);

        // Live goal so it homes to a moving picker; a parabola on top makes it a jump, back to 0 on arrival.
        Vector3 goal = _target != null ? _target.position + Vector3.up * _height : _startPos;
        Vector3 pos = Vector3.Lerp(_startPos, goal, k);
        pos.y += (_height * collectArcMultiple) * 4f * k * (1f - k);   // arc scales with the pick height
        transform.position = pos;

        transform.localScale = Vector3.Lerp(_startScale, _startScale * collectEndScale, k);

        if (k >= 1f) Finish();
    }

    void Finish()
    {
        Picked?.Invoke(this);
        // TODO(inventory): add the resource to the backpack here when that system exists.
        // TODO(fx): spawn a few pickup particles at the picker here.
        Destroy(gameObject);
    }
}
