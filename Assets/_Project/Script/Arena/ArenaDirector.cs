using System.Collections.Generic;
using UnityEngine;

// Runs the arena's wave schedule. Born with a run, dies with it.
//
// NOT A SpawnZone, AND NOT A BIGGER ONE. A zone holds a POPULATION: it fills a patch of world to capacity and
// tops it back up, so the map has the same number of creatures living in it whether you are there or not.
// This runs a SCHEDULE: what arrives, when, and how many — aimed at the player, from off screen. One is a
// world that has animals in it; the other is a night somebody wrote. No configuration of the first becomes
// the second, which is why this is its own thing.
//
// THE SCHEDULE IS THE WHOLE DIFFICULTY DESIGN, and it is authored rather than derived. Nothing else about a
// run gets harder — no hidden multipliers on health or damage — so a night that plays wrong is a row on the
// arena's asset, and the fix is a number somebody can picture before they type it.
//
// EACH WAVE KEEPS ITS OWN TALLY. A row is one day, so its count is spent once and cannot come back: the
// arrays here are indexed by row and live exactly as long as the run does. That is also what makes overlapping
// rows work — two waves in the same hour are simply two tallies draining at once.
//
// SPAWNED ON AN AUTHORED RING, and that only works because the camera cannot be zoomed. The wheel used to
// move it, which is exactly what made a typed radius meaningless — right at one zoom level and either on
// screen or absurdly far at every other. With the shot fixed, "just past the corner of the screen" is a
// distance somebody can measure once and write down, and the code does not have to re-derive every frame what
// the camera can see.
//
// STRAGGLERS ARE MOVED, NOT REPLACED. A body left far behind is brought round to the ring again instead of
// being destroyed and re-sent, and the difference is not an optimisation: a wave's count is how many bodies it
// is worth, so destroying one and sending another would spend the same wave twice and quietly turn thirty into
// forty. Moving also keeps the monster's HP — the player who fought it down to a sliver and then ran should
// meet the same wounded thing, not a fresh one.
//
// COUNTED BY OWNERSHIP, not by looking around. The bodies it made are the bodies it counts, wherever they
// have wandered to — same rule SpawnZone keeps, and for a stronger reason here: a hunter goes wherever the
// player goes, so a radius query would lose track of exactly the ones that matter.
public class ArenaDirector
{
    readonly ArenaConfig _arena;
    readonly RunClock _clock;
    readonly EnemySpawner _spawner;
    readonly IPlayer _player;
    readonly TerrainGrid _grid;

    readonly List<Damageable> _alive = new List<Damageable>();

    readonly int[] _spent;      // bodies this wave has already sent, per row
    readonly float[] _pending;  // fractional bodies owed, per row — a slow wave still arrives

    // How many tries to find a spot before giving this body up for the frame. Cheap tries — a random angle
    // and two lookups — and giving up is the right failure: one body arriving a frame late is invisible,
    // where forcing one onto an invalid cell is a monster standing inside a wall.
    const int PlacementTries = 12;

    // How far past the outer ring a body has to be before it is brought round again, as a multiple of the
    // ring itself. Derived rather than authored: the ring already IS the arena's statement of "out of sight",
    // and a second number would be free to disagree with it. Comfortably beyond anything on screen, so the
    // move is never seen.
    const float RecycleRingMultiple = 1.5f;

    // Giving up is quiet by design — but giving up EVERY time, for a wave that is due, is a map that cannot
    // hold the ring at all, and that must not be quiet. Reported once per run: the fix is authoring (a bigger
    // arena, a smaller camera distance), not something to re-read every frame.
    bool _placementReported;

    public int Alive => _alive.Count;

    public ArenaDirector(ArenaConfig arena, RunClock clock, EnemySpawner spawner, IPlayer player, TerrainGrid grid)
    {
        _arena = arena;
        _clock = clock;
        _spawner = spawner;
        _player = player;
        _grid = grid;

        int rows = arena.waves != null ? arena.waves.Length : 0;
        _spent = new int[rows];
        _pending = new float[rows];

        if (_grid == null)
            Debug.LogError($"[{nameof(ArenaDirector)}] arena '{arena.Id}' has no {nameof(TerrainGrid)} — " +
                           "nothing can be placed, so no wave will arrive.");
    }

    public void Tick(float deltaTime)
    {
        Forget();
        if (_grid == null || !_player.Exists || _arena.waves == null) return;

        Recycle();

        int day = _clock.Day;
        float hour = _clock.Hour;

        for (int i = 0; i < _arena.waves.Length; i++)
            RunWave(i, _arena.waves[i], day, hour, deltaTime);
    }

    void RunWave(int index, ArenaWave wave, int day, float hour, float deltaTime)
    {
        int owed = wave.count - _spent[index];
        if (owed <= 0) return;
        if (day != wave.day || !wave.Contains(hour)) return;
        if (string.IsNullOrWhiteSpace(wave.enemyId)) return;

        // A lump lands whole the moment its hour arrives; a window meters the count out across itself.
        // Seconds rather than hours, because that is what deltaTime is in — the day's length is the bridge,
        // and it is the arena's own (see ArenaConfig.dayLengthSeconds), so a short test day compresses the
        // schedule instead of breaking it.
        float windowHours = wave.WindowHours;
        if (windowHours <= 0f)
        {
            _pending[index] = owed;
        }
        else
        {
            float windowSeconds = windowHours / 24f * _arena.dayLengthSeconds;
            _pending[index] += wave.count / Mathf.Max(0.0001f, windowSeconds) * deltaTime;
        }

        // The cap holds a wave, it does not cancel it — the debt stays on _pending and pays out as soon as
        // there is room. Clamped so a long spell at the ceiling cannot bank a minute's worth and dump it all
        // in the frame after one dies.
        if (_pending[index] > 1f && _alive.Count >= _arena.maxAlive) _pending[index] = 1f;

        while (_pending[index] >= 1f && _spent[index] < wave.count && _alive.Count < _arena.maxAlive)
        {
            _pending[index] -= 1f;
            if (Send(wave.enemyId)) _spent[index]++;
            else break;   // nowhere to put it this frame; keep the debt and try again next one
        }
    }

    // Bring stragglers back round. A hunter never stops coming, but a slow one loses ground every time the
    // player crosses the map, and a tail of monsters trailing behind is a tail that costs the alive budget
    // without ever being part of the fight.
    void Recycle()
    {
        float threshold = Mathf.Max(_arena.spawnRing.x, _arena.spawnRing.y) * RecycleRingMultiple;
        float thresholdSqr = threshold * threshold;
        Vector3 player = _player.Position;

        foreach (var body in _alive)
        {
            if (body == null) continue;

            Vector3 d = body.transform.root.position - player;
            d.y = 0f;
            if (d.sqrMagnitude < thresholdSqr) continue;

            // No spot free this frame is fine: it is already far away, and it will be just as far next frame.
            if (FindSpot(out Vector3 at)) body.transform.root.position = at;
        }
    }

    // Everything this run put into the world, taken out of it. The run's monsters belong to the run — leaving
    // one standing when the overworld map arrives is the one thing an arena must never leak.
    public void Dispose()
    {
        foreach (var body in _alive)
            if (body != null) Object.Destroy(body.transform.root.gameObject);
        _alive.Clear();
    }

    bool Send(string enemyId)
    {
        if (!FindSpot(out Vector3 at))
        {
            ReportPlacementFailure();
            return false;
        }

        var body = _spawner.Spawn(enemyId, at, Quaternion.identity);
        if (body == null) return false;   // the spawner already said why

        var damageable = body.GetComponentInChildren<Damageable>(true);
        if (damageable != null) _alive.Add(damageable);
        return true;
    }

    // Said once, and it names the numbers rather than the symptom: "no monsters appeared" points nowhere,
    // where "the ring is 26 units out and the map is 32 across" points straight at the map.
    void ReportPlacementFailure()
    {
        if (_placementReported) return;
        _placementReported = true;

        float span = _grid != null ? Mathf.Max(_grid.Width, _grid.Height) * _grid.CellSize : 0f;

        Debug.LogWarning($"[{nameof(ArenaDirector)}] arena '{_arena.Id}': a wave is due but no walkable spot " +
                         $"was found in {PlacementTries} tries. Spawn ring is {_arena.spawnRing.x:0.#}–" +
                         $"{_arena.spawnRing.y:0.#} units from the player at {_player.Position}, and the map " +
                         $"is {span:0.#} units across. Either the ring reaches past the edge of the arena, or " +
                         "it is landing on water/walls. Said once per run.");
    }

    // A walkable cell somewhere on the authored ring. Random angle each try rather than a swept one: a sweep
    // makes arrivals march around the player in a circle, which reads as a machine.
    bool FindSpot(out Vector3 at)
    {
        at = default;

        Vector3 centre = _player.Position;
        float near = Mathf.Min(_arena.spawnRing.x, _arena.spawnRing.y);
        float far = Mathf.Max(_arena.spawnRing.x, _arena.spawnRing.y);

        for (int i = 0; i < PlacementTries; i++)
        {
            float angle = Random.value * Mathf.PI * 2f;
            float radius = Mathf.Lerp(near, far, Random.value);
            Vector3 candidate = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;

            if (!_grid.WorldToCell(candidate, out int cx, out int cy)) continue;   // off the map
            if (!_grid.IsWalkable(cx, cy)) continue;                               // wall, water, a pit

            at = _grid.CellToWorld(cx, cy);
            return true;
        }
        return false;
    }

    // Bodies leave the list once they are dead or gone. SWEPT, not unsubscribed from a death event: this
    // already ticks every frame, IsAlive is the same answer the event would carry, and a subscription would
    // mean remembering to take it back off every body the run destroys on its way out.
    void Forget()
    {
        for (int i = _alive.Count - 1; i >= 0; i--)
            if (_alive[i] == null || !_alive[i].IsAlive) _alive.RemoveAt(i);
    }
}
