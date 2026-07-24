using System.Collections.Generic;
using UnityEngine;
using Lean.Pool;

// A slow homing soul-fire, in three phases:
//   Spawning - over spawnTime the glow sprite fades + scales up to its authored look and the flame plays.
//   Flying   - drifts to the nearest hostile (CombatWorld hash) within the caster's range, easing down
//              near it; re-targets if its mark dies first.
//   Bursting - on contact (damage lands here) OR when nothing is in range, the flame stops, the burst
//              particle fires once, and the glow blooms then fades to match it; then it despawns.
// Pooled - every field is (re)set in Launch, so a recycled flame never carries the last shot's state.
[DisallowMultipleComponent]
public class SoulFire : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] SpriteRenderer glow;      // the light; fades/scales in on spawn, blooms on burst
    [SerializeField] ParticleSystem flame;     // the trailing fire; runs through the flight
    [SerializeField] ParticleSystem burst;     // one-shot explosion on the end (set Play On Awake OFF)

    [Header("Timing")]
    [SerializeField] float spawnTime = 0.1f;   // glow fades + scales in over this
    [SerializeField] float slowRadius = 1.5f;  // eases the drift down within this of the target
    [SerializeField] float riseHeight = 2f;    // with no target, drifts up this far before it bursts
    [SerializeField] float burstTime = 0.35f;  // glow bloom + fade, ~ the explosion length
    [SerializeField] float burstScale = 1.6f;  // glow scale multiplier at the burst peak
    [SerializeField] float hitPadding = 0.15f; // land a touch past the target's hit circle

    enum Phase { Spawning, Flying, Rising, Bursting }
    Phase _phase;
    float _t;
    float _riseStartY;
    float _burstScale;     // glow bloom target for the current burst (bigger when it self-destructs)
    Vector3 _glowScale;    // authored glow scale (the "current level" to grow into)
    Color _glowColor;

    Transform _caster;
    float _range, _damage, _speed, _knockback;
    int _team;
    IDamageable _target;
    readonly List<IDamageable> _found = new List<IDamageable>();

    // Cache the authored glow look once, before any shot mutates it - otherwise a pooled flame would
    // re-cache the alpha 0 it faded to last burst and stay invisible forever.
    void Awake()
    {
        if (glow != null)
        {
            _glowScale = glow.transform.localScale;
            _glowColor = glow.color;
        }
    }

    public void Launch(Transform caster, float range, int team, float damage, float speed, float knockback)
    {
        _caster = caster;
        _range = range;
        _team = team;
        _damage = damage;
        _speed = speed;
        _knockback = knockback;
        _target = null;
        _phase = Phase.Spawning;
        _t = 0f;

        if (glow != null)
        {
            glow.transform.localScale = Vector3.zero;
            SetGlowAlpha(0f);
        }
        if (flame != null) { flame.Clear(); flame.Play(); }
        if (burst != null) burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        switch (_phase)
        {
            case Phase.Spawning: Spawning(dt); break;
            case Phase.Flying:   Flying(dt);   break;
            case Phase.Rising:   Rising(dt);   break;
            default:             Bursting(dt); break;
        }
    }

    void Spawning(float dt)
    {
        _t += dt;
        float k = spawnTime > 0f ? Mathf.Clamp01(_t / spawnTime) : 1f;
        if (glow != null)
        {
            glow.transform.localScale = _glowScale * k;
            SetGlowAlpha(_glowColor.a * k);
        }
        if (k >= 1f) _phase = Phase.Flying;
    }

    void Flying(float dt)
    {
        if (_target == null || !_target.IsAlive)
            _target = FindNearest();

        if (_target == null) { StartRise(); return; }   // nothing in range -> drift up, then burst

        Vector3 d = _target.Position - transform.position;
        d.y = 0f;
        float dist = d.magnitude;

        if (dist <= _target.HitRadius + hitPadding)
        {
            _target.TakeDamage(_damage, this);           // damage lands only on arrival
            if (_knockback > 0f && dist > 1e-4f)
                _target.ApplyKnockback((d / dist) * _knockback);   // shove along the flame's travel
            StartBurst(burstScale);
            return;
        }

        float v = _speed * (slowRadius > 0f ? Mathf.Clamp(dist / slowRadius, 0.3f, 1f) : 1f);
        transform.position += (d / dist) * (v * dt);
    }

    // No target to chase: drift straight up, then burst harmlessly at the top.
    void StartRise()
    {
        _phase = Phase.Rising;
        _riseStartY = transform.position.y;
    }

    void Rising(float dt)
    {
        transform.position += Vector3.up * (_speed * dt);
        if (transform.position.y - _riseStartY >= riseHeight) StartBurst(burstScale * 2f);   // wasted shot pops bigger
    }

    void StartBurst(float glowScale)
    {
        _phase = Phase.Bursting;
        _t = 0f;
        _burstScale = glowScale;
        if (flame != null) flame.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (burst != null) { burst.Clear(); burst.Play(); }
    }

    void Bursting(float dt)
    {
        _t += dt;
        float k = burstTime > 0f ? Mathf.Clamp01(_t / burstTime) : 1f;
        if (glow != null)
        {
            glow.transform.localScale = _glowScale * Mathf.Lerp(1f, _burstScale, k);
            SetGlowAlpha(_glowColor.a * (1f - k));
        }
        if (k >= 1f) LeanPool.Despawn(gameObject);
    }

    // Nearest hostile to the flame, within the caster's range. TODO: prefer enemies over trees once
    // enemies exist (see Docs) - for now any non-allied Damageable in range qualifies.
    IDamageable FindNearest()
    {
        CombatWorld.Instance.Rebuild();
        CombatWorld.Instance.Overlap(_caster != null ? _caster.position : transform.position, _range, _team, _found);

        IDamageable best = null;
        float bestSq = float.MaxValue;
        Vector3 from = transform.position;
        for (int i = 0; i < _found.Count; i++)
        {
            Vector3 d = _found[i].Position - from;
            float sq = d.x * d.x + d.z * d.z;
            if (sq < bestSq) { bestSq = sq; best = _found[i]; }
        }
        return best;
    }

    void SetGlowAlpha(float a)
    {
        var c = _glowColor;
        c.a = a;
        glow.color = c;
    }
}
