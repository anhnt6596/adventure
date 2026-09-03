using UnityEngine;
using VContainer;

// A door to an arena, standing on an overworld map: step in and a run starts there.
//
// IT IS ONLY A DOOR. Which arena, and nothing else — the rank, the monsters, the days and the payout are all
// facts about the place on the far side, so they live on the ArenaConfig. One arena, one gate.
//
// AND IT IS A ONE-WAY DOOR. There is no gate on the far side: a run is left by clearing it or by dying, so
// stepping in is a commitment. ArenaRunner reports it at load if an arena was authored with a way out.
//
// NOT A Portal, THOUGH IT LOOKS LIKE ONE FROM THE OUTSIDE. A Portal moves the player between places in one
// continuous world; this opens and closes a whole lifetime — its own clock, its own wallet, its own
// everything — and hands the player back afterwards. Sharing a base class would put "am I the kind that
// starts a run" inside Portal.
//
// THE ARENA IS DRAGGED IN, NOT NAMED BY ID. MapService takes a map id because only one map prefab may be in
// memory at a time; an ArenaConfig is a small asset with no such rule, and PayGate (ResourceDef) and
// SpawnZone (enemy list) already author their data by reference. A typed id would be a fourth name to keep in
// sync with three others, and nothing in the Inspector would say it had gone stale.
public class ArenaGate : InteractZone
{
    [Tooltip("Where this goes. Drag the arena's asset in — everything about the run is in there.")]
    [SerializeField] ArenaConfig arena;

    [Tooltip("Where the player is put down when the run ends, as a spawn point index in THIS map. Author one " +
             "beside the gate: coming back out somewhere else is a map mistake, not a runtime problem.")]
    [SerializeField, Min(0)] int returnSpawnIndex;

    ArenaRunner _runner;

    [Inject]
    public void ConstructGate(ArenaRunner runner) => _runner = runner;

    protected override void Start()
    {
        base.Start();

        if (arena == null)
            Debug.LogError($"[{nameof(ArenaGate)}] '{name}' has no arena — walking into it does nothing.", this);

        WarnIfYouComeBackOutInsideTheGate();
    }

    // A run that puts the player back down INSIDE this zone starts the next one on the same frame, forever.
    // Checked here rather than defended against at runtime: it is an authoring mistake with an obvious fix
    // (move the spawn point off the gate), and a guard would hide it instead of showing it — the project's
    // rule is to fail loud at wiring boundaries.
    void WarnIfYouComeBackOutInsideTheGate()
    {
        var map = GetComponentInParent<Map>();
        if (map == null || map.SpawnPointCount == 0) return;   // Map itself already says so if it has none

        var back = map.GetSpawnPoint(returnSpawnIndex);
        if (back != null && Contains(back.SpawnPosition))
            Debug.LogError($"[{nameof(ArenaGate)}] '{name}' sends the player back to spawn point " +
                           $"{returnSpawnIndex}, which is INSIDE its own zone — every run would open the next " +
                           "one the moment it ends. Move that spawn point clear of the gate.", this);
    }

    public override void OnActorEnter(MCController actor) => _runner?.Enter(arena, returnSpawnIndex);
}
