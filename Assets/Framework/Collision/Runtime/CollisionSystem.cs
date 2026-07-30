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
    TerrainQuery _query;

    // The combined view of the world: the tilemap plus every bridge over it. Everything that asks the world a
    // question goes through this, including the collision step — the tilemap alone would push a body straight
    // off a deck, because the riverbank's walls are still there underneath it.
    public TerrainQuery Query => _query ??= new TerrainQuery(terrain);

    public CollisionWorld World => _world ??= new CollisionWorld(Query);

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

    // A bridge joining and leaving, the same self-service way a body does. The tilemap is not told.
    public void Register(IWalkVolume volume) => Query.Add(volume);

    public void Unregister(IWalkVolume volume) => Query.Remove(volume);

    // Point the world at a newly loaded map's terrain. New bodies then use its pass mask.
    public void SetTerrain(TerrainGrid t)
    {
        terrain = t;
        Query.SetTerrain(t);
        World.RefreshPassMasks();
    }

    // ---- world queries -----------------------------------------------------
    //
    // The one door gameplay goes through, and it deliberately says nothing about how many geometries are behind
    // it. No terrain = no restriction, so a scene without a map still plays.

    // Is a world point on walkable ground (not water / off the map, or on a bridge over either)?
    public bool IsWalkable(Vector3 world) => Query.IsWalkable(world);

    // Can a body of `radius` walk straight from `from` to `to`? passMask < 0 uses the terrain's default.
    public bool CanMove(Vector3 from, Vector3 to, float radius = 0f, int passMask = -1)
        => Query.CanMove(from, to, radius, passMask);

    // Does a straight line cross a wall? Line of sight, a thrown arc's clearance, a hitscan shot.
    public bool IntersectsWall(Vector3 from, Vector3 to, int passMask = -1)
        => Query.IntersectsWall(from, to, passMask);

    // Could anything walk between these two points at all, however long the way round? O(1) — it compares
    // connected components of the tilemap, unioned by the bridges that join them. A raised drawbridge makes it
    // false the moment it raises.
    public bool Reachable(Vector3 from, Vector3 to) => Query.Reachable(from, to);

    void LateUpdate() => World.Step(iterations);
}
