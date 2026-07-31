using System;
using Lean.Pool;
using UnityEngine;
using VContainer;

// A price on a piece of the world: a slot you stand in and feed until it is full. Progress is kept, so you
// can bring half the wood now and the rest three trips later, and what it unlocks switches on the moment the
// last piece goes in and stays on for good.
//
// FED A PIECE AT A TIME, not paid in one lump. That is what makes it a slot rather than a shop till: the
// resources visibly leave you and land in the hole, the number above it climbs, and a gate you cannot yet
// afford is still progress rather than a locked door. It also means a player never has to know the price
// before walking up — standing there and watching it fill tells them.
//
// GENERIC ON PURPOSE, and it already has a second consumer waiting: Docs/DESIGN.md says some jump points are
// locked behind "reach a level, pay currency, etc.". So this gate knows nothing about bridges — it turns
// GameObjects on and off. A bridge is simply the first thing put in that list.
//
// SetActive IS the whole mechanism, and it is enough because Bridge was already built to survive it:
// VolumeActive tests isActiveAndEnabled, and OnDisable takes the deck out of TerrainQuery. So one call hides
// the art AND removes the walkable geometry AND makes Reachable report the far bank as unreachable again —
// no interface for the gate to talk through, and nothing for a second kind of unlockable to implement.
public class PayGate : InteractZone
{
    [Tooltip("Save key. Type something and never change it: renaming, moving or reordering the gate must " +
             "not lose what a player already fed into it.")]
    [SerializeField] string id;

    [Tooltip("What it costs. Empty = free, which is a usable way to author a gate that is only a switch.")]
    [SerializeField] ResourceCost[] cost;

    [Tooltip("Switched on once the price is met, off until then. A Bridge object here is hidden AND " +
             "unwalkable while it is off, because disabling it takes its deck out of the walkable geometry.")]
    [SerializeField] GameObject[] unlocks;

    [Tooltip("The opposite: shown until the price is met, hidden after. The slot itself, its rim, a sign — " +
             "whatever says 'something goes here'.")]
    [SerializeField] GameObject[] preview;

    [Header("Feeding")]
    [Tooltip("Seconds between pieces while standing in the slot. Small: this is a stream, not a payment.")]
    [SerializeField, Min(0.01f)] float feedInterval = 0.1f;

    [Tooltip("Pieces per tick. Raise it for a gate priced in the hundreds so filling it is not a wait.")]
    [SerializeField, Min(1)] int feedBatch = 1;

    [Tooltip("Optional: the piece that flies from the player into the slot. Same prefab a drop uses.")]
    [SerializeField] PickupFlyVisual feedVisual;

    PayGateSystem _gates;
    MCController _actor;
    float _feedTimer;

    public string Id => id;

    // Anything to read this — a view, an editor check — asks the gate, and the gate works it out from the
    // deposits. Nothing caches it, so editing a price in the inspector is instantly reflected.
    public bool Paid
    {
        get
        {
            if (cost == null) return true;
            foreach (var c in cost)
                if (Needed(c) > 0) return false;
            return true;
        }
    }

    public ResourceCost[] Cost => cost;
    public int Deposited(ResourceCost c) => c.resource != null ? _gates.Deposited(id, c.resource.Id) : 0;

    // What is still missing of one line of the price.
    public int Needed(ResourceCost c)
        => c.resource == null ? 0 : Mathf.Max(0, c.amount - Deposited(c));

    public event Action Changed;   // a piece went in, or the gate opened

    [Inject]
    public void ConstructGate(PayGateSystem gates) => _gates = gates;

    protected override void Start()
    {
        base.Start();

        if (string.IsNullOrWhiteSpace(id))
            Debug.LogError($"[{nameof(PayGate)}] '{name}' has no id — it cannot remember what was fed into it, " +
                           "so it will reset on every load.", this);

        Apply();   // a loaded save may already have filled this
    }

    public override void OnActorEnter(MCController actor)
    {
        _actor = actor;
        _feedTimer = 0f;   // the first piece goes in immediately, so stepping in always does something visible
    }

    public override void OnActorExit(MCController actor)
    {
        if (_actor == actor) _actor = null;
    }

    // The zone only reports entering and leaving, so standing in it is this component's own business.
    void Update()
    {
        if (_actor == null || _gates == null || Paid) return;

        _feedTimer -= Time.deltaTime;
        if (_feedTimer > 0f) return;
        _feedTimer = feedInterval;

        var inventory = _actor.GetComponentInChildren<Picker>()?.Inventory;
        if (inventory == null) return;

        Feed(inventory);
    }

    // One tick: take from the first line of the price that is both unmet and in the bag. One line at a time
    // rather than all of them at once, so a gate priced in two resources fills visibly in two stages instead
    // of two interleaved streams that read as noise.
    void Feed(Inventory inventory)
    {
        if (cost == null) return;

        foreach (var c in cost)
        {
            int need = Needed(c);
            if (need <= 0) continue;

            int take = Mathf.Min(feedBatch, need, inventory.Get(c.resource));
            if (take <= 0) continue;   // nothing of this kind on us — try the next line

            inventory.Remove(c.resource, take);
            _gates.Deposit(id, c.resource.Id, take);
            SpawnFeedVisual();

            if (Paid) Apply();
            Changed?.Invoke();
            return;
        }
    }

    void SpawnFeedVisual()
    {
        if (feedVisual == null || _actor == null) return;
        var fly = LeanPool.Spawn(feedVisual, _actor.transform.position, Quaternion.identity);
        fly.Launch(transform, 0.6f);   // out of the player, into the slot — the pickup flight, reversed
    }

    // The two halves of one switch. `preview` is not decoration: a slot you can SEE from the far bank is what
    // turns "there is a river here" into "there is a way across, and here is what it wants". Nothing to aim
    // at is the opposite of what an exploration game wants from a gate.
    void Apply()
    {
        bool open = Paid;

        if (unlocks != null)
            foreach (var go in unlocks)
                if (go != null) go.SetActive(open);

        if (preview != null)
            foreach (var go in preview)
                if (go != null) go.SetActive(!open);
    }

#if UNITY_EDITOR
    // What this gate is wired to, at a glance: yellow to what it buys, grey to what it replaces.
    void OnDrawGizmosSelected()
    {
        Draw(unlocks, new Color(1f, 0.85f, 0.2f, 0.9f));
        Draw(preview, new Color(0.6f, 0.62f, 0.68f, 0.9f));
    }

    void Draw(GameObject[] list, Color c)
    {
        if (list == null) return;
        Gizmos.color = c;
        foreach (var go in list)
            if (go != null) Gizmos.DrawLine(transform.position, go.transform.position);
    }
#endif
}

// One line of a price. Same shape as DeathDrop, for the same reason: a list of (thing, count) is what every
// cost and every reward in this game turns out to be.
[Serializable]
public struct ResourceCost
{
    public ResourceDef resource;
    [Min(1)] public int amount;
}
