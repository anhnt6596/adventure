using UnityEngine;

public class CollisionBody : MonoBehaviour, ICollisionBody
{
    [SerializeField] CollisionShape shape = CollisionShape.Circle;
    [SerializeField] float radius = 0.4f;                   // Circle
    [SerializeField] Vector2 size = new Vector2(1f, 1f);    // Rect: full width (x) x depth (z), axis-aligned

    [Tooltip("0 = immovable (wall, boss). Higher = harder to shove aside.")]
    [SerializeField, Min(0f)] float mass = 1f;

    [Tooltip("How fast a knockback shove bleeds off (units/s²). Higher = shorter slide.")]
    [SerializeField, Min(0f)] float knockbackDrag = 8f;

    Vector3 _knockVel;   // decaying velocity from knockback shoves; integrated in Update, resolved by the world

    // Self-registers with the one CollisionSystem: a body always lives in a prefab (character, map, pooled
    // drop) that can't serialize a scene reference, so it reaches the system through its Instance instead of
    // waiting to be bound. OnEnable/OnDisable also handle a pooled body toggling on respawn.
    void OnEnable() => CollisionSystem.Instance?.Register(this);
    void OnDisable() => CollisionSystem.Instance?.Unregister(this);

    public Vector3 Position { get => transform.position; set => transform.position = value; }
    public CollisionShape Shape => shape;
    public Vector2 HalfExtents => size * 0.5f;

    // Circle: its radius. Rect: bounding radius (half-diagonal) — used for broad-phase + terrain.
    public float Radius => shape == CollisionShape.Rect ? (size * 0.5f).magnitude : radius;

    public float InvMass => mass > 0f ? 1f / mass : 0f;
    public int PassMask { get; private set; } = ~0;

    public void SetPassMask(int mask) => PassMask = mask;

    // Lets an owner drive mass from its stats/config instead of the serialized default.
    public void SetMass(float m) => mass = Mathf.Max(0f, m);

    public bool IsKnocked => _knockVel.sqrMagnitude > 0.0001f;

    // An attack shoves the body along `impulse`; dividing by mass makes the same hit fling a light body far
    // and a heavy one little, and an immovable body (mass 0 → InvMass 0) not at all. The slide is corrected
    // against walls and other bodies by CollisionWorld.Step, like any other movement this frame.
    public void AddImpulse(Vector3 impulse) => _knockVel += impulse * InvMass;

    void Update()
    {
        if (!IsKnocked) return;

        // Cap the per-frame step to the body radius. The world resolves terrain once per frame and only
        // pushes a body back out while its centre is within `radius` of a shore/wall segment; a bigger step
        // (strong shove, or a frame hitch) would jump clean past that band and land inside water/a wall with
        // nothing to pull it back. Clamping keeps every step inside the resolve's reach, so it piles up at
        // the edge instead of tunnelling through — the drag still bleeds the speed off at the same rate.
        Vector3 step = _knockVel * Time.deltaTime;
        float maxStep = Radius * 0.9f;
        float mag = step.magnitude;
        if (mag > maxStep) step *= maxStep / mag;
        transform.position += step;

        _knockVel = Vector3.MoveTowards(_knockVel, Vector3.zero, knockbackDrag * Time.deltaTime);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.5f);
        var p = transform.position;

        if (shape == CollisionShape.Rect)
        {
            Vector2 h = size * 0.5f;
            Vector3 a = p + new Vector3(-h.x, 0f, -h.y);
            Vector3 b = p + new Vector3( h.x, 0f, -h.y);
            Vector3 c = p + new Vector3( h.x, 0f,  h.y);
            Vector3 d = p + new Vector3(-h.x, 0f,  h.y);
            Gizmos.DrawLine(a, b); Gizmos.DrawLine(b, c); Gizmos.DrawLine(c, d); Gizmos.DrawLine(d, a);
            return;
        }

        const int steps = 24;
        for (int i = 0; i < steps; i++)
        {
            float a0 = i / (float)steps * Mathf.PI * 2f;
            float a1 = (i + 1) / (float)steps * Mathf.PI * 2f;
            Gizmos.DrawLine(
                p + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius,
                p + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius);
        }
    }
#endif
}
