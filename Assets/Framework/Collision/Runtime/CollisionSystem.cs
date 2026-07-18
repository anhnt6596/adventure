using UnityEngine;

// Runs after every mover has written its position, so it corrects the frame's result rather than
// racing it.
public class CollisionSystem : MonoBehaviour
{
    [SerializeField] TerrainGrid terrain;
    [SerializeField, Range(1, 8)] int iterations = 2;

    CollisionWorld _world;

    public CollisionWorld World => _world ??= new CollisionWorld(terrain);

    public void Register(ICollisionBody body)
    {
        if (body is CollisionBody b && terrain != null) b.SetPassMask(terrain.DefaultPassMask);
        World.Add(body);
    }

    public void Unregister(ICollisionBody body) => World.Remove(body);

    // Point the world at a newly loaded map's terrain. New bodies then use its pass mask.
    public void SetTerrain(TerrainGrid t)
    {
        terrain = t;
        World.SetTerrain(t);
    }

    void LateUpdate() => World.Step(iterations);
}
