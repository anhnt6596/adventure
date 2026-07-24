using System;
using UnityEngine;

// The shape of a spawn zone, tested in the zone's LOCAL space (y ignored, so the zone can sit at any height).
// An abstraction so SpawnZone's bake never hard-codes a shape — Circle and Box today; a painted tile mask can
// slot in later as another subclass without touching the zone. [SerializeReference] on the zone keeps the
// chosen concrete type serialized.
[Serializable]
public abstract class SpawnArea
{
    public abstract bool Contains(Vector3 local);

#if UNITY_EDITOR
    // Drawn in local space — the caller sets Gizmos.matrix to the zone's transform.
    public abstract void DrawGizmo();
#endif
}

[Serializable]
public class CircleArea : SpawnArea
{
    [Min(0f)] public float radius = 5f;

    public override bool Contains(Vector3 local)
    {
        local.y = 0f;
        return local.sqrMagnitude <= radius * radius;
    }

#if UNITY_EDITOR
    public override void DrawGizmo()
    {
        const int seg = 48;
        Vector3 prev = new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg;
            Vector3 next = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
#endif
}

[Serializable]
public class BoxArea : SpawnArea
{
    public Vector2 size = new Vector2(10f, 10f);   // x = width, y = depth (world Z)

    public override bool Contains(Vector3 local)
        => Mathf.Abs(local.x) <= size.x * 0.5f && Mathf.Abs(local.z) <= size.y * 0.5f;

#if UNITY_EDITOR
    public override void DrawGizmo() => Gizmos.DrawWireCube(Vector3.zero, new Vector3(size.x, 0f, size.y));
#endif
}
