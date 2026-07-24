using UnityEngine;

public class CollisionBody : MonoBehaviour, ICollisionBody
{
    [SerializeField] CollisionShape shape = CollisionShape.Circle;
    [SerializeField] float radius = 0.4f;                   // Circle
    [SerializeField] Vector2 size = new Vector2(1f, 1f);    // Rect: full width (x) x depth (z), axis-aligned

    [Tooltip("0 = immovable (wall, boss). Higher = harder to shove aside.")]
    [SerializeField, Min(0f)] float mass = 1f;

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
