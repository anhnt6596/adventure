using UnityEngine;

// Throws a dropped item out with velocity, then lets it settle and become pickable.
//
// The game is 2D-in-3D: logic (position, collision, pickup) lives on the ground plane, so the ROOT only
// ever moves on XZ. The arc HEIGHT is purely visual — it drives the art child's local Y and never
// touches the root, so it contributes nothing to logic. While airborne the root carries a CollisionBody
// (mass + collision); on landing (or once picked) that body switches off and it becomes an inert pickup.
[RequireComponent(typeof(Pickable))]
[DisallowMultipleComponent]
public class FlyingPickup : MonoBehaviour
{
    [Header("Horizontal — logic, on the ground plane")]
    [SerializeField] float minSpeed = 3f;
    [SerializeField] float maxSpeed = 6f;
    [SerializeField] float drag = 8f;            // ground friction; higher = shorter throw
    [SerializeField] float spreadDegrees = 35f;

    [Header("Height — visual only, on the art")]
    [SerializeField] Transform art;              // drag the art child; its local Y shows the hop
    [SerializeField] float jumpSpeed = 4.5f;     // initial upward speed of the hop
    [SerializeField] float gravity = 20f;

    Pickable _pickable;
    CollisionBody _body;
    Vector3 _artBaseLocal;

    Vector3 _hVel;     // horizontal velocity (XZ) — logic
    float _vVel;       // vertical velocity — visual only
    float _height;     // current art height above the ground
    bool _flying;

    void Awake()
    {
        _pickable = GetComponent<Pickable>();
        TryGetComponent(out _body);
        if (art != null) _artBaseLocal = art.localPosition;
        else Debug.LogError($"[{nameof(FlyingPickup)}] no art assigned — drag the art child in so the hop has something to move.", this);
    }

    // dir = rough fling direction on the ground plane.
    public void Launch(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            dir = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
        dir.Normalize();
        dir = Quaternion.Euler(0f, Random.Range(-spreadDegrees, spreadDegrees), 0f) * dir;

        _hVel = dir * Random.Range(minSpeed, maxSpeed);
        _vVel = jumpSpeed;
        _height = 0f;
        _flying = true;
        _pickable.SetPickable(false);   // no grabbing mid-flight

        if (_body != null) _body.enabled = true;   // mass + collision on; the body self-registers via OnEnable
    }

    void Update()
    {
        if (!_flying) return;
        float dt = Time.deltaTime;

        // Horizontal = logic: slide the root on the ground plane with friction; collision resolves it.
        transform.position += _hVel * dt;
        _hVel = Vector3.MoveTowards(_hVel, Vector3.zero, drag * dt);

        // Height = visual: a hop on the art's local Y, never on the root.
        _vVel -= gravity * dt;
        _height += _vVel * dt;
        if (_height <= 0f && _vVel < 0f)   // coming down through the ground → landed
        {
            _height = 0f;
            Land();
        }
        if (art != null) art.localPosition = _artBaseLocal + Vector3.up * _height;
    }

    void Land()
    {
        _flying = false;
        _hVel = Vector3.zero;
        _pickable.SetPickable(true);                 // landed → pickable
        if (_body != null) _body.enabled = false;    // drop mass + collision, become inert
    }
}
