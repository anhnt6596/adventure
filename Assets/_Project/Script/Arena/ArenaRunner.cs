using System;
using Core.UI;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using VContainer.Unity;

// Opens an arena, runs it, closes it. One run at a time, and it is the only thing that knows a run is
// happening.
//
// IT LIVES IN THE GAME SCOPE AND OWNS THE RUN SCOPE. Asking "when does this die?" — the way ARCHITECTURE.md
// says to decide where something goes — a runner outlives every run it opens, while a clock, a wallet and a
// pile of half-built defences all die with one. So they are two scopes, and this one holds the other.
//
// THERE IS NO "AM I IN AN ARENA" FLAG ANYWHERE ELSE. The run scope existing IS the answer, and it is one
// object's business whether it does. An arena does not know a run is being held on it; a monster does not
// know it was spawned in one; the lighting does not know whose clock it is reading.
//
// THE ORDER AT THE END IS THE DESIGN, not a sequence of steps that happened to work:
//
//     tally -> respawn if dead -> warp back to the overworld -> tear down the run -> pay
//
// The tally is taken before anything is torn down, because what the run was worth is a fact about the run. A
// dead body is rebuilt BEFORE the trip out, because a corpse cannot be placed at a spawn point. And nothing
// is paid until the run's own scope is gone: the reward belongs to the world, and handing it over while the
// run's wallet is still open is exactly how the two get confused.
public class ArenaRunner : ITickable
{
    readonly IObjectResolver _container;
    readonly IMapService _maps;
    readonly IPlayer _player;
    readonly PlayerSystem _players;
    readonly ActiveTimeOfDay _timeOfDay;
    readonly EnemySpawner _spawner;
    readonly IUISystem _ui;
    readonly CardLibrary _cards;

    IScopedObjectResolver _scope;
    ArenaConfig _arena;
    RunClock _clock;
    ArenaDirector _director;
    RunLevel _level;
    RunUpgrades _upgrades;
    Damageable _watched;

    string _returnMapId;
    int _returnSpawnIndex;
    bool _busy;      // a warp is in flight — the run neither ticks nor ends while the world is being swapped

    public bool InRun => _clock != null;
    public ArenaConfig Arena => _arena;
    public RunClock Clock => _clock;
    public RunLevel Level => _level;

    // What a kill in this run is worth, and the ONLY door into the run's levelling. Nothing reaches RunLevel
    // except through here, so "experience earned in an arena never touches the save" is one place to check
    // rather than a rule everybody has to remember.
    public void AwardExp(int amount) => _level?.Award(amount);

    // Fired once the run is fully closed and the player is standing back outside. Whatever pays for a run
    // listens here rather than being called from inside it, so the run has no idea what it is worth.
    public event Action<ArenaResult> Ended;

    [Inject]
    public ArenaRunner(IObjectResolver container, IMapService maps, IPlayer player, PlayerSystem players,
                       ActiveTimeOfDay timeOfDay, EnemySpawner spawner, IUISystem ui, CardLibrary cards)
    {
        _container = container;
        _maps = maps;
        _player = player;
        _players = players;
        _timeOfDay = timeOfDay;
        _spawner = spawner;
        _ui = ui;
        _cards = cards;
    }

    // `returnSpawnIndex` is the spawn point in the CURRENT map the player steps back out at — authored on the
    // gate, the way Portal already authors where it sends you. Coming out somewhere other than where you went
    // in is a map-authoring mistake, not something to solve with a remembered position.
    public void Enter(ArenaConfig arena, int returnSpawnIndex)
    {
        if (InRun || _busy) return;

        if (arena == null)
        {
            Debug.LogError($"[{nameof(ArenaRunner)}] asked to open a null arena.");
            return;
        }
        if (string.IsNullOrWhiteSpace(arena.mapId))
        {
            Debug.LogError($"[{nameof(ArenaRunner)}] arena '{arena.Id}' has no map id — it is a place with " +
                           "nowhere to stand.", arena);
            return;
        }

        EnterAsync(arena, returnSpawnIndex).Forget();
    }

    async UniTaskVoid EnterAsync(ArenaConfig arena, int returnSpawnIndex)
    {
        _busy = true;

        _returnMapId = _maps.CurrentMapId;
        _returnSpawnIndex = returnSpawnIndex;

        _arena = arena;
        _clock = new RunClock(arena.days, arena.dayLengthSeconds, arena.startHour);
        _level = new RunLevel(arena.expToNext);

        // The scope before the map, because the map is injected THROUGH it — anything authored into an arena
        // that wants the run's services would otherwise be built against a run that does not exist yet.
        _scope = _container.CreateScope(b =>
        {
            b.RegisterInstance(arena);    // where we are and on what terms
            b.RegisterInstance(_clock);   // how far into it we are
            b.RegisterInstance(_level);   // how strong we have got since walking in
        });

        // Before the warp, not after: the swap is a cut, so the arena should already be lit by its own hour
        // the first frame it is on screen.
        _timeOfDay.Use(_clock);

        await _maps.WarpAsync(arena.mapId, arena.spawnIndex, _scope);

        WatchPlayer();
        HealToFull();
        StomachFor(true);
        WarnAboutWaysOut();

        // After the warp, because the arena's terrain only exists once the map is built — and the director
        // places every body on it.
        // After the player is standing and healed: the first card can land on the body immediately.
        _upgrades = new RunUpgrades(_cards, _level, _player, _ui, arena.handSize);

        _director = new ArenaDirector(arena, _clock, _spawner, _player,
                                      UnityEngine.Object.FindFirstObjectByType<TerrainGrid>());
        _busy = false;
    }

    // EVERY RUN STARTS FROM SCRATCH, and the body is the one thing that carries over from outside. Level,
    // upgrades, wallet and buildings never existed before this moment because the run scope did not; HP and
    // the stomach are older than the run, so they are the two that have to be told.
    void HealToFull()
    {
        if (_watched != null) _watched.Heal(_watched.MaxHp);
    }

    // Hunger is an ARENA mechanic: it is the pressure that stops a player camping one corner, and outside a
    // run there is nothing to eat, nothing to kill you and nothing for a draining bar to mean. So the stomach
    // runs exactly as long as the run does.
    void StomachFor(bool running)
    {
        var stomach = _player.Current != null ? _player.Current.GetComponentInChildren<Hunger>() : null;
        if (stomach == null) return;
        if (running) stomach.BeginRun();
        else stomach.EndRun();
    }

    // AN ARENA HAS ONE WAY IN AND NO WAY OUT: you leave by clearing it or by dying, and nothing authored into
    // the map may offer a third option. A Portal in here would hand the player a free exit that skips the
    // tally, and an ArenaGate would open a run inside a run.
    //
    // Checked by looking, not prevented by design, because the mistake is made in the Inspector hours before
    // it goes wrong and the symptom (a run that ends with no reward) points nowhere near the cause.
    void WarnAboutWaysOut()
    {
        foreach (var portal in UnityEngine.Object.FindObjectsByType<Portal>(FindObjectsSortMode.None))
            Debug.LogError($"[{nameof(ArenaRunner)}] arena '{_arena.Id}' has a Portal ('{portal.name}') in it — " +
                           "that is a way out that skips the tally. An arena is left by clearing it or by " +
                           "dying, and by nothing else.", portal);

        foreach (var gate in UnityEngine.Object.FindObjectsByType<ArenaGate>(FindObjectsSortMode.None))
            Debug.LogError($"[{nameof(ArenaRunner)}] arena '{_arena.Id}' has an {nameof(ArenaGate)} " +
                           $"('{gate.name}') in it — a run cannot open a run. Gates belong on overworld maps.", gate);
    }

    public void Tick()
    {
        if (_clock == null || _busy) return;

        _clock.Tick(Time.deltaTime);
        _director?.Tick(Time.deltaTime);

        // The clock is read AFTER the horde ticks, so the frame a run is cleared on is a frame that still
        // played normally. Ending first would leave one tick of the last day silently missing.
        if (_clock.Cleared) End(true);
    }

    void End(bool cleared)
    {
        if (_clock == null || _busy) return;
        EndAsync(cleared).Forget();
    }

    async UniTaskVoid EndAsync(bool cleared)
    {
        _busy = true;

        var result = new ArenaResult(_arena.Id, _clock.Day, cleared);
        StopWatchingPlayer();
        StomachFor(false);

        // Before the warp out, and BEFORE any respawn: everything the run put on the character comes back
        // off. A modifier left tagged would walk out of the arena in the player's own body — the one way a
        // run's power could survive the run.
        _upgrades?.Dispose();
        _upgrades = null;
        _level = null;

        // Same reason for the horde: a monster still standing when the overworld map arrives is the one thing
        // an arena must never leak.
        _director?.Dispose();
        _director = null;

        // A dead body cannot be put down at a spawn point. Under possession a respawn IS a fresh body for the
        // same character, which is what PlayerSystem does when asked for the one already selected.
        if (!cleared) Respawn();

        await _maps.WarpAsync(_returnMapId, _returnSpawnIndex);

        _timeOfDay.Release(_clock);
        _clock = null;
        _arena = null;

        _scope?.Dispose();   // the run's clock, wallet and everything built in there goes with it
        _scope = null;

        _busy = false;
        Ended?.Invoke(result);
    }

    void Respawn()
    {
        string id = _player.Current != null ? _player.Current.Id : null;
        if (string.IsNullOrEmpty(id))
        {
            Debug.LogError($"[{nameof(ArenaRunner)}] the run ended in death with no player body to rebuild — " +
                           "the world is left without a character.");
            return;
        }
        _players.SwitchTo(id);
    }

    // Death is the other way out, so the runner watches for it directly rather than waiting to be told. The
    // body is whatever PlayerSystem last spawned; a run does not switch characters, so binding once is enough.
    void WatchPlayer()
    {
        _watched = _player.Current != null ? _player.Current.GetComponentInChildren<Damageable>() : null;
        if (_watched == null)
        {
            Debug.LogError($"[{nameof(ArenaRunner)}] no {nameof(Damageable)} on the player — a run can then " +
                           "only end by surviving it, never by dying.");
            return;
        }
        _watched.Died += OnPlayerDied;
    }

    void StopWatchingPlayer()
    {
        if (_watched != null) _watched.Died -= OnPlayerDied;
        _watched = null;
    }

    void OnPlayerDied(object source) => End(false);
}

// What a finished run was worth, handed out once it is closed. Days survived rather than won/lost is the
// number that matters: clearing is just surviving all of them, and dying on the last night still bought
// nearly everything dying on the first did not.
public readonly struct ArenaResult
{
    public readonly string ArenaId;
    public readonly int DaysSurvived;
    public readonly bool Cleared;

    public ArenaResult(string arenaId, int daysSurvived, bool cleared)
    {
        ArenaId = arenaId;
        DaysSurvived = daysSurvived;
        Cleared = cleared;
    }
}
