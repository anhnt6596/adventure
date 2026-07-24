using UnityEngine;

// Runs after every mover has written its position, so it corrects the frame's result rather than
// racing it. The one collision world for the game — a body reaches it through Instance and self-registers,
// so a prefab (which can't serialize a scene reference) needs no one to bind it.
[DefaultExecutionOrder(-100)]   // Awake before any CollisionBody.OnEnable, so Instance is set to register into
public class CollisionSystem : MonoBehaviour
{
    public static CollisionSystem Instance { get; private set; }

    [SerializeField] TerrainGrid terrain;
    [SerializeField, Range(1, 8)] int iterations = 2;

    CollisionWorld _world;

    public CollisionWorld World => _world ??= new CollisionWorld(terrain);

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

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
