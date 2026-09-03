using UnityEngine;
using VContainer;

// What killing this is worth, handed over when it dies. The mirror of DropOnDeath, and for the reason
// Damageable states outright: it just fires Died, and what a death PROVIDES is somebody else's business.
//
// NOT SPLIT INTO A DOER AND AN ADAPTER the way drops are. Dropable exists because dropping is real work —
// spawn, fling, land — that a chest or a depleted node will want without dying. Paying experience is one call
// to a service; anything else that ever wants to pay calls ExperienceSystem directly.
//
// ONLY WHAT THE PLAYER KILLS PAYS. A predator eating a critter is the world going about its business and must
// not level anybody, so the test is on the source of the killing blow.
//
// A component rather than a hook inside EnemySpawner, even though the spawner has everything it would need:
// death is going to grow listeners — a sound, a kill counter for a quest, a bestiary entry — and each of
// those is its own component here, or all of them are subscriptions piled into a class whose job is to turn
// an id into a body.
[RequireComponent(typeof(Damageable))]
[DisallowMultipleComponent]
public class ExpOnDeath : MonoBehaviour
{
    ExperienceSystem _exp;
    ArenaRunner _arena;
    Damageable _damageable;
    Unit _unit;

    IDeathExpConfig Cfg => _unit != null ? _unit.DamageableConfig as IDeathExpConfig : null;

    [Inject]
    public void Construct(ExperienceSystem exp, ArenaRunner arena)
    {
        _exp = exp;
        _arena = arena;
    }

    void Awake()
    {
        _damageable = GetComponent<Damageable>();
        _unit = GetComponentInParent<Unit>();
    }

    void OnEnable()  { if (_damageable != null) _damageable.Died += OnDied; }
    void OnDisable() { if (_damageable != null) _damageable.Died -= OnDied; }

    // Start, like Dropable's: a unit resolves its config during injection, which runs after Awake, so there
    // is nothing to check any earlier.
    void Start()
    {
        if (Cfg == null)
            Debug.LogError($"[{nameof(ExpOnDeath)}] its Unit's config is not an {nameof(IDeathExpConfig)}, so " +
                           "killing this pays nothing. Props deliberately are not — see the interface.", this);

        if (_exp == null)
            Debug.LogError($"[{nameof(ExpOnDeath)}] no {nameof(ExperienceSystem)} injected — spawn this through " +
                           $"{nameof(EnemySpawner)}, which injects through a scope that has it.", this);
    }

    void OnDied(object source)
    {
        if (_exp == null || !KilledByPlayer(source)) return;

        var cfg = Cfg;
        if (cfg == null) return;

        // TWO LEDGERS, AND THE KILL BELONGS TO ONE OF THEM. Inside an arena the bounty buys power for THAT
        // run and nothing else; outside, it is the world's. Docs/GATE_RUN.md turns on this line: run
        // experience that leaked into the save would make a safe arena farmable, which is the one thing the
        // whole design refuses.
        if (_arena != null && _arena.InRun) _arena.AwardExp(cfg.Exp);
        else _exp.Award(cfg.Exp);

        // The bestiary is the WORLD's either way, and that is not an exception to the rule above — a first
        // cannot be farmed, so it is safe to pay from inside a run. It is also the thing that rewards going
        // to arenas full of creatures you have never met.
        if (cfg.FirstKillExp > 0 && _unit != null) _exp.AwardOnce($"kind:{_unit.Id}", cfg.FirstKillExp);
    }

    // The source is the component that dealt the blow — a ShapeAttack hanging off the swinging body, or the
    // caster a SoulFire was launched by. Both sit under whoever owns them, so one walk up the parents answers
    // "was this the player" for every attack in the game, and for any attack added later.
    static bool KilledByPlayer(object source)
        => source is Component c && c.GetComponentInParent<MCController>() != null;
}
