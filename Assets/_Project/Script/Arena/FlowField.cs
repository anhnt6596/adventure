using UnityEngine;
using VContainer.Unity;

// Which way to walk to reach the player, from anywhere on the map. One breadth-first sweep out from the
// player's cell, and every monster reads the cell it is standing in.
//
// ONE FIELD FOR THE WHOLE HORDE, NOT A PATH EACH. In a survivors-like every monster is chasing the same
// target, so eighty A* searches would be eighty near-identical answers to one question. A sweep costs the
// same whether one creature reads it or two hundred — the price is set by the size of the map, and a 32x32
// map is a thousand cells.
//
// FOUR NEIGHBOURS TO BUILD IT, EIGHT TO READ IT. Building on four keeps the sweep a plain BFS with every step
// the same length — no weights, no priority queue, no chance of the diagonal shortcut being wrong. Reading on
// eight is what makes the result smooth: the direction is the whole downhill GRADIENT over the neighbourhood,
// not "which of my four sides is lowest", so it comes out as a heading rather than as one of four.
//
// NO CORNER CUTTING. A diagonal only counts if both of the orthogonals beside it are open — otherwise bodies
// clip the inside of every corner and walk through the join between two walls.
//
// IT IS NOT A PATH AND CANNOT BE ONE. There is no route to follow, no waypoint to reach, nothing per-monster
// to store or invalidate. A body that gets knocked across the map is already following the field correctly
// the next frame, which is the other half of why this beats a path here.
//
// IT ASKS TerrainQuery, NOT THE TILEMAP. A bridge is not terrain — it is a deck laid over water that adds
// walkable ground without changing a single cell (see Docs/DESIGN.md). Reading TerrainGrid.IsWalkable
// directly builds a field in which no bridge in the game exists, and the horde gathers on the bank of a river
// it could have crossed. The same query is what the collision uses, so what the field believes and what a
// body can actually do cannot disagree.
//
// REBUILT ON A TIMER, and only while a run is bound. Four times a second is far more often than a walking
// player can invalidate it, and between sweeps the field is a little stale in exactly the direction that does
// not matter: the last cell the player was in is next to the one they are in now.
public class FlowField : ITickable
{
    // How often the sweep runs while anything is chasing. Not authored: it is a property of how fast a person
    // walks, not of any one arena, and it is the kind of number that only ever gets tuned by feel once.
    const float RebuildInterval = 0.25f;

    TerrainGrid _grid;
    TerrainQuery _query;   // the REAL walkability: terrain plus every deck laid over it
    IPlayer _player;

    int[] _dist;        // steps from the player, -1 = unreachable or not walkable
    int[] _queue;       // BFS frontier, reused
    int _width, _height;

    float _age;
    int _walkVersion = -1;   // the world's walkable shape when the field was last built

    public bool Ready => _grid != null && _query != null && _dist != null;

    // Bound by ArenaRunner when a run opens, and unbound when it closes. Not resolved from the scene: the
    // field must stop existing the moment the map it describes does, and "the runner says so" is a clearer
    // rule than "it noticed the terrain changed".
    public void Bind(TerrainGrid grid, IPlayer player)
    {
        _grid = grid;
        _player = player;
        _query = grid != null ? CollisionSystem.Instance?.Query : null;

        if (_grid == null || _query == null) { _dist = null; return; }

        _width = _grid.Width;
        _height = _grid.Height;

        int n = _width * _height;
        if (_dist == null || _dist.Length != n)
        {
            _dist = new int[n];
            _queue = new int[n];
        }
        Rebuild();
    }

    public void Unbind() => Bind(null, null);

    // Force the next Tick to sweep. Rarely needed: the world announces its own changes through WalkVersion,
    // so a bridge lowering or a wall going up is picked up without anybody remembering to say so.
    public void MarkDirty() => _age = RebuildInterval;

    public void Tick()
    {
        if (!Ready || _player == null || !_player.Exists) return;

        // A DECK LOWERED OR A WALL BROKEN CHANGES THE ANSWER EVERYWHERE, so it earns a sweep the instant it
        // happens rather than up to a quarter of a second later — that is exactly the moment a player is
        // watching to see whether the horde takes the new route.
        if (_query.WalkVersion != _walkVersion) { Rebuild(); return; }

        _age += Time.deltaTime;
        if (_age < RebuildInterval) return;

        Rebuild();
    }

    void Rebuild()
    {
        if (!Ready || _player == null || !_player.Exists) return;
        if (!_grid.WorldToCell(_player.Position, out int px, out int py)) return;

        // The player standing on something unwalkable (mid-knockback over a pit, a spawn point a tile out) is
        // not worth a special case: the field simply keeps the last good one, and the next sweep fixes it.
        if (!Walkable(px, py)) return;

        _age = 0f;
        _walkVersion = _query.WalkVersion;
        for (int i = 0; i < _dist.Length; i++) _dist[i] = -1;

        int start = py * _width + px;
        _dist[start] = 0;

        int head = 0, tail = 0;
        _queue[tail++] = start;

        while (head < tail)
        {
            int at = _queue[head++];
            int x = at % _width, y = at / _width;
            int next = _dist[at] + 1;

            Step(x + 1, y, next, ref tail);
            Step(x - 1, y, next, ref tail);
            Step(x, y + 1, next, ref tail);
            Step(x, y - 1, next, ref tail);
        }
    }

    void Step(int x, int y, int cost, ref int tail)
    {
        if ((uint)x >= (uint)_width || (uint)y >= (uint)_height) return;

        int at = y * _width + x;
        if (_dist[at] >= 0 || !Walkable(x, y)) return;

        _dist[at] = cost;
        _queue[tail++] = at;
    }

    // Downhill, on the ground plane. Zero when there is nothing to read — off the map, on an unreachable
    // island, or already standing on the player's own cell — and the caller falls back to heading straight.
    public Vector3 DirectionAt(Vector3 world)
    {
        if (!Ready || !_grid.WorldToCell(world, out int x, out int y)) return Vector3.zero;

        int here = Here(x, y);
        if (here < 0) return Vector3.zero;

        // THE WHOLE NEIGHBOURHOOD, WEIGHTED BY HOW MUCH CLOSER IT IS. Summing the downhill steps rather than
        // picking the single lowest is what turns eight headings into one smooth direction — two neighbours a
        // step closer pull equally and the body leaves between them, which is the diagonal a person would walk.
        Vector3 sum = Vector3.zero;
        for (int oy = -1; oy <= 1; oy++)
        for (int ox = -1; ox <= 1; ox++)
        {
            if (ox == 0 && oy == 0) continue;

            // No corner cutting: a diagonal is only open if you could have walked round it either way.
            if (ox != 0 && oy != 0 && (Here(x + ox, y) < 0 || Here(x, y + oy) < 0)) continue;

            int there = Here(x + ox, y + oy);
            if (there < 0) continue;

            int drop = here - there;
            if (drop <= 0) continue;

            sum += new Vector3(ox, 0f, oy).normalized * drop;
        }

        return sum.sqrMagnitude > 1e-6f ? sum.normalized : Vector3.zero;
    }

    int Here(int x, int y)
        => (uint)x < (uint)_width && (uint)y < (uint)_height ? _dist[y * _width + x] : -1;

    // Asked at the cell's CENTRE. A deck answers analytically rather than by cell, so a bridge covering most
    // of a cell makes that cell standable, and its middle is the fairest single point to ask about.
    bool Walkable(int x, int y) => _query.IsWalkable(_grid.CellToWorld(x, y));

    // Could a body of this width walk straight there? Handed to TerrainQuery rather than sampled here: it is
    // the same test the collision runs, so a monster can never decide on a line its own body will then refuse
    // — and it accounts for how wide that body is, so nothing tries to squeeze down a gap narrower than it.
    //
    // This is what keeps a hunter from looking like it is on rails. In an open arena the answer is yes almost
    // always and it walks straight at you; the field only takes over when something is genuinely in the way.
    public bool CanWalkStraight(Vector3 from, Vector3 to, float radius)
        => _query != null && _query.CanMove(from, to, radius);
}
