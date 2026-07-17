using UnityEngine;

public interface ICollisionBody
{
    Vector3 Position { get; set; }
    float Radius { get; }

    // 0 = immovable. Mass is a design knob ("how hard to shove"), not physics: a boss is a wall,
    // a slime is not.
    float InvMass { get; }
}
