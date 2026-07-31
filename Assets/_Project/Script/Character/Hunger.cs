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
    ICharacterStats _stats;
    IHungerConfig _cfg;
    Damageable _health;
    float _value;
    bool _started;

    public event System.Action Changed;

    [Inject]
    public void Construct(ICharacterStats stats, IHungerConfig cfg)
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
        _value = Max;
        _started = true;
        Changed?.Invoke();
    }

    void Update()
    {
        if (!_started) return;
        if (_health != null && !_health.IsAlive) return;   // no digesting while dead

        float dt = Time.deltaTime;

        // The cap can move under us — an upgrade equipped, a buff expiring — so clamp before draining
        // rather than trusting whatever the last frame left behind.
        float max = Max;
        if (_value > max) { _value = max; Changed?.Invoke(); }

        if (_value > 0f)
        {
            _value = Mathf.Max(0f, _value - DrainRate * dt);
            Changed?.Invoke();
        }

        if (_health == null) return;

        // Starving outranks well fed, and they cannot both be true anyway — this is just the order that reads.
        if (_value <= 0f) _health.TakeDamage(_cfg.StarveDamage * dt, this);
        else if (IsWellFed) _health.Heal(_cfg.WellFedHeal * dt);
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
