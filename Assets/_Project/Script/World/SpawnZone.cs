using System;
using System.Collections.Generic;
using UnityEngine;

// A spawn zone placed on a map: a shape over the terrain, baked down to the walkable cells inside it. This is
// the authoring + bake side (Docs/SPAWN.md, step 1); the runtime spawn loop (warm/cold, capacity, respawn
// debt, gating) is a separate concern and isn't here yet. All setup lives on the zone — move to an SO later
// if it needs reuse across maps.
[DisallowMultipleComponent]
public class SpawnZone : MonoBehaviour
{
    [SerializeReference] SpawnArea area = new CircleArea();

    [Header("Grid")]
    [SerializeField] TerrainGrid grid;                 // auto-found from the map if left empty
    [SerializeField, Min(0)] int clearance = 1;        // walkable margin (cells) kept from any wall/water edge

    [Header("Spawning — consumed by the runtime loop (not built yet)")]
    [SerializeField] EnemyWeight[] enemies;            // roll one per spawn by weight; a single entry = one kind
    [SerializeField, Min(0)] int capacity = 5;
    [SerializeField] bool warm = true;                 // fill to capacity on map load, vs trickle in cold
    [SerializeField, Min(0f)] float respawnDelay = 8f;      // per-death debt delay
    [SerializeField, Min(0f)] float wipeLockDuration = 30f; // extra lock after the whole zone is cleared
    [SerializeField, Min(0f)] float minPlayerDist = 8f;     // never spawn a cell closer than this to the player
    [SerializeField] bool spawnOffCameraOnly = true;
    [SerializeField, Min(0f)] float activeRadius = 40f;     // stop ticking past this from the player (CPU cap)

    [SerializeField, HideInInspector] Vector2Int[] cells;   // baked: walkable cells inside the area

    public SpawnArea Area => area;
    public IReadOnlyList<Vector2Int> Cells => cells;
    public int CellCount => cells != null ? cells.Length : 0;

    // The map's grid: an explicit ref, else the one on this object's parent, else anywhere under the map root.
    public TerrainGrid ResolveGrid()
    {
        if (grid != null) return grid;
        var found = GetComponentInParent<TerrainGrid>();
        if (found == null && transform.root != null) found = transform.root.GetComponentInChildren<TerrainGrid>(true);
        return found;
    }

#if UNITY_EDITOR
    // Editor-only: recompute the walkable cells inside the area. Called from the Bake button in SpawnZoneEditor.
    public void Bake()
    {
        var g = ResolveGrid();
        if (g == null) { Debug.LogError($"[{nameof(SpawnZone)}] '{name}' found no TerrainGrid on the map.", this); return; }
        if (area == null) { Debug.LogError($"[{nameof(SpawnZone)}] '{name}' has no area shape.", this); return; }

        var found = new List<Vector2Int>();
        for (int y = 0; y < g.Height; y++)
            for (int x = 0; x < g.Width; x++)
            {
                if (!Spawnable(g, x, y)) continue;
                var local = transform.InverseTransformPoint(g.CellToWorld(x, y));
                if (area.Contains(local)) found.Add(new Vector2Int(x, y));
            }

        UnityEditor.Undo.RecordObject(this, "Bake Spawn Cells");
        cells = found.ToArray();
        UnityEditor.EditorUtility.SetDirty(this);

        if (cells.Length == 0)
            Debug.LogError($"[{nameof(SpawnZone)}] '{name}' baked 0 spawnable cells — move/resize it over walkable land, or lower clearance.", this);
    }

    // Walkable AND surrounded by walkable within `clearance`, so a spawn never lands against a wall/water edge.
    bool Spawnable(TerrainGrid g, int x, int y)
    {
        for (int dy = -clearance; dy <= clearance; dy++)
            for (int dx = -clearance; dx <= clearance; dx++)
                if (!g.IsWalkable(x + dx, y + dy)) return false;
        return true;
    }

    void OnDrawGizmos()
    {
        if (area == null) return;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.45f, 0.15f, 0.9f);
        area.DrawGizmo();
        Gizmos.matrix = Matrix4x4.identity;
    }

    void OnDrawGizmosSelected()
    {
        if (cells == null || cells.Length == 0) return;
        var g = ResolveGrid();
        if (g == null) return;

        Gizmos.color = new Color(1f, 0.55f, 0.2f, 0.28f);
        var size = new Vector3(g.CellSize, 0.01f, g.CellSize) * 0.85f;
        foreach (var c in cells)
            Gizmos.DrawCube(g.CellToWorld(c.x, c.y) + Vector3.up * 0.05f, size);
    }
#endif
}

[Serializable]
public struct EnemyWeight
{
    public string id;
    [Min(0)] public int weight;
}
