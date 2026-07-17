using UnityEngine;

public class CollisionBody : MonoBehaviour, ICollisionBody
{
    [SerializeField] float radius = 0.4f;

    [Tooltip("0 = immovable (wall, boss). Higher = harder to shove aside.")]
    [SerializeField, Min(0f)] float mass = 1f;

    [SerializeField] CollisionSystem system;

    void OnEnable()
    {
        if (system != null) system.Register(this);
    }

    void OnDisable()
    {
        if (system != null) system.Unregister(this);
    }

    public Vector3 Position { get => transform.position; set => transform.position = value; }
    public float Radius => radius;
    public float InvMass => mass > 0f ? 1f / mass : 0f;
    public int PassMask { get; private set; } = ~0;

    // The default comes from the terrain's own walkable flags; a swimming or flying body overrides
    // it once, a buff toggles single bits.
    public void SetPassMask(int mask) => PassMask = mask;
    public void AllowTerrain(int terrainId) => PassMask |= TerrainSet.BitOf(terrainId);
    public void DenyTerrain(int terrainId) => PassMask &= ~TerrainSet.BitOf(terrainId);
    public bool IsOn(int terrainId, TerrainGrid terrain)
        => terrain.WorldToCell(transform.position, out int x, out int y)
           && terrain.TerrainAt(x, y) == terrainId;

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.5f);
        var p = transform.position;
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
