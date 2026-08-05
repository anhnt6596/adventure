using UnityEngine;
using VContainer;

// The stomach: the character's second vital, sitting beside HP. It drains on its own, it is the ONLY thing
// food fills, and it is the whole food budget for a trip — nothing carries rations, so how far you can go is
// how much you are holding plus what you find on the way.
//
// It is deliberately not an inventory with one entry. A bag of food would need a capacity, and a capacity on
// something you carry raises "why is only food limited"; a stomach raises nothing, because a stomach obviously
// has a size. That framing is also what deletes the last of the inventory UI: full simply means the next meal
// is left where it lies, so there is never a choice to present.
//
// EVERYTHING HERE MOVES ONCE A SECOND, not once a frame. The numbers were always written per second, so a
// tick applies them whole instead of a frame's sliver of them: fullness steps down by exactly HungerDrain,
// starving and being well fed move HP by their whole per-second share of it. That makes the rate a
// player can feel match the number in the config, keeps HP moving in readable steps rather than in noise
// below a decimal place, and drops the HUD from a refresh every frame to one a second.
//
// IT DRIVES HP AT BOTH ENDS, and that is what makes it a stat rather than a timer:
//
//     full            > wellFed   ->  HP regenerates    (being well fed is how you heal)
//     somewhere between          ->  nothing happens    (the state you spend most of a trip in)
//     empty           == 0       ->  HP drains          (starving kills, slowly)
//
// The middle band is the point. Docs/DESIGN.md: the player should RARELY look at this bar while exploring —
// it is a trip budget, not a nagging timer. Tune the drain so the bar only ever says "this trip ends here".
[DisallowMultipleComponent]
public class Hunger : MonoBehaviour
{
    // One second. Not a knob: the config's numbers are all "per second", so this is the unit they are
    // written in rather than a tuning value of its own.
    const float TickInterval = 1f;

    // The CONCRETE stats, not the read-only interface, and that is a declaration rather than a convenience:
    // this class puts a modifier on the drain (see DrainPaused), so it is one of the few things allowed to
    // move a stat. Anything that only reads takes ICharacterStats and cannot.
    MainCharStats _stats;
    IHungerConfig _cfg;
    Damageable _health;
    float _value;
    float _tick;
    bool _started;

    public event System.Action Changed;

    [Inject]
    public void Construct(MainCharStats stats, IHungerConfig cfg)
    {
        _stats = stats;
        _cfg = cfg;
    }

    // Read live off the Stat, never cached: a stomach upgrade has to apply the moment it is equipped, and
    // caching it here would be a second copy to keep in step with the one the stat already owns.
    public float Max => _stats != null ? Mathf.Max(0f, _stats.MaxHunger.Value) : 0f;
    public float DrainRate => _stats != null ? Mathf.Max(0f, _stats.HungerDrain.Value) : 0f;
    public float Value => _value;
    public float Fraction => Max > 0f ? Mathf.Clamp01(_value / Max) : 0f;
    public bool IsFull => _value >= Max;
    public bool IsEmpty => _value <= 0f;
    public float SpaceLeft => Mathf.Max(0f, Max - _value);

    // Above the well-fed line, so HP is climbing. Exposed for the HUD, which marks the line on the bar — a
    // threshold you cannot see is a rule the player has to be told instead of shown.
    public bool IsWellFed => _cfg != null && Fraction > _cfg.WellFedFraction;
    public float WellFedFraction => _cfg != null ? _cfg.WellFedFraction : 1f;

    // Filled in Start, like Damageable's HP: injection runs after Awake, so Max is not known any earlier.
    void Start()
    {
        _health = GetComponentInChildren<Damageable>(true);
        if (_cfg == null || _stats == null)
        {
            Debug.LogError($"[{nameof(Hunger)}] not injected — add this GameObject to GameScope's Auto Inject list.", this);
            return;
        }
        _value = Max * Mathf.Clamp01(_cfg.StartFullness);
        _started = true;
        Changed?.Invoke();
    }

    void Update()
    {
        if (!_started) return;
        if (_health != null && !_health.IsAlive) return;   // no digesting while dead

        // The cap can move under us — an upgrade equipped, a buff expiring — so clamp every frame rather
        // than waiting for a tick to notice.
        float max = Max;
        if (_value > max) { _value = max; Changed?.Invoke(); }

        _tick += Time.deltaTime;
        if (_tick < TickInterval) return;

        // A loop, not an if: a stalled frame must not swallow the seconds it spanned. Time.deltaTime is
        // capped by Time.maximumDeltaTime, so in practice this runs once.
        while (_tick >= TickInterval)
        {
            _tick -= TickInterval;
            Step();
        }
    }

    void Step()
    {
        if (_value > 0f)
        {
            _value = Mathf.Max(0f, _value - DrainRate);
            Changed?.Invoke();
        }

        if (_health == null) return;

        // Starving outranks well fed, and they cannot both be true anyway — this is just the order that reads.
        // Draining first means the tick that empties you is also the tick that starts hurting.
        if (_value <= 0f) _health.TakeDamage(_health.MaxHp * _cfg.StarveShare, this);
        else if (IsWellFed) _health.Heal(_health.MaxHp * _cfg.WellFedHealShare);
    }

    // Eat. Takes what fits and says how much that was; the rest is the caller's problem, which for a pickup
    // means staying on the ground until there is room for it.
    public float Eat(float amount)
    {
        if (!_started || amount <= 0f) return 0f;

        float eaten = Mathf.Min(amount, SpaceLeft);
        if (eaten <= 0f) return 0f;

        _value += eaten;
        Changed?.Invoke();
        return eaten;
    }

    // Stops the stomach emptying, by putting a -100% modifier on the drain Stat rather than by keeping a
    // flag this class has to remember to check. That is the same road a real effect would take — a safe
    // zone, a trait, a full-belly buff — so nothing here is a path only a cheat can walk.
    public bool DrainPaused
    {
        get => _drainMod != null;
        set
        {
            if (value == DrainPaused || _stats == null) return;
            if (value)
            {
                // -1 is -100% of the base drain: a multiplier is a share now, not a factor, so 0 would be a
                // modifier that does nothing (see StatModifier).
                //
                // It is a real pause only while nothing else touches this stat. Shares add up, so a +20% from
                // hungry gear would leave a fifth of the drain running, and a flat Add never went through the
                // share at all. The day either exists, pausing has to stop being a modifier.
                _drainMod = new StatModifier(-1f, StatModKind.Mul, this);
                _stats.Modifiable(StatId.HungerDrain)?.Add(_drainMod);
            }
            else
            {
                _stats.Modifiable(StatId.HungerDrain)?.RemoveBySource(this);
                _drainMod = null;
            }
        }
    }

    StatModifier _drainMod;

    // For a death penalty or a debuff to take fullness away directly.
    public void Drain(float amount)
    {
        if (!_started || amount <= 0f) return;
        _value = Mathf.Max(0f, _value - amount);
        Changed?.Invoke();
    }

    public void Fill()
    {
        if (!_started) return;
        _value = Max;
        Changed?.Invoke();
    }
}
