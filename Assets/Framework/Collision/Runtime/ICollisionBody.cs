using UnityEngine;

public enum CollisionShape { Circle, Rect }

public interface ICollisionBody
{
    Vector3 Position { get; set; }

    // Broad-phase + terrain use this as the body's reach: a circle's radius, or a rect's bounding radius.
    float Radius { get; }

    CollisionShape Shape { get; }

    // Half width (x) and half depth (z) for a Rect; unused for a Circle. Axis-aligned — no rotation.
    Vector2 HalfExtents { get; }

    // 0 = immovable. Mass is a design knob ("how hard to shove"), not physics: a boss is a wall,
    // a slime is not.
    float InvMass { get; }

    // Bit per terrain id this body may enter. A buff opens water by setting its bit; nothing about
    // the terrain changes.
    int PassMask { get; }
}
