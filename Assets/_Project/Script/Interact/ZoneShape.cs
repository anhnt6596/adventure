using System;
using UnityEngine;

public enum ZoneKind { Circle, Rect }

// A rectangle or circle on the ground (XZ), evaluated in the owner's local space so rotation works.
[Serializable]
public struct ZoneShape
{
    public ZoneKind kind;
    public float radius;     // Circle
    public Vector2 size;     // Rect: x = width (local X), y = depth (local Z)

    public bool Contains(Transform owner, Vector3 worldPoint)
    {
        Vector3 p = owner.InverseTransformPoint(worldPoint);
        if (kind == ZoneKind.Circle)
            return p.x * p.x + p.z * p.z <= radius * radius;
        return Mathf.Abs(p.x) <= size.x * 0.5f && Mathf.Abs(p.z) <= size.y * 0.5f;
    }

    // Worst-case reach from the centre — the query radius the spatial hash needs to still find us.
    public float BoundingRadius =>
        kind == ZoneKind.Circle ? radius : new Vector2(size.x, size.y).magnitude * 0.5f;

    public static ZoneShape DefaultCircle => new ZoneShape { kind = ZoneKind.Circle, radius = 1f };
}
