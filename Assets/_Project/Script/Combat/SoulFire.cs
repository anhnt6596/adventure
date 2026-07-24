using System.Collections.Generic;
using UnityEngine;
using Lean.Pool;

// A slow homing soul-fire, in phases:
//   Spawning - over spawnTime the glow sprite fades + scales up to its authored look and the flame plays.
//   Flying   - drifts to a hostile (CombatWorld hash), easing down near it. The player's flame re-targets the
//              nearest if its mark dies; an enemy's locks its first mark and chases only that.
//   Bursting - on contact (damage lands here) the flame stops, the burst particle fires once, the glow blooms
//              then fades, and it despawns.
//   Rising / Fading - with NO target to chase: an unlocked flame drifts up and pops (Rising); a locked one
//              just stops and lets the trail fade out over fadeTime (Fading) — no rise, no burst.
// ONE prefab for both — the caster's TEAM decides the behaviour (player = team 1). Pooled: every field is
// (re)set in Launch, so a recycled flame never carries the last shot's state.
[DisallowMultipleComponent]
public class SoulFire : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] SpriteRenderer glow;      // the light; fades/scales in on spawn, blooms on burst
    [SerializeField] ParticleSystem flame;     // the trailing fire; runs through the flight
    [SerializeField] ParticleSystem burst;     // one-shot explosion on the end (set Play On Awake OFF)

    [Header("Timing")]
    [SerializeField] float spawnTime = 0.1f;   // glow fades + scales in over this
    [SerializeField] float speed = 6f;         // flight speed — the flame's own, not the caster's
    [SerializeField] float slowRadius = 1.5f;  // eases the drift down within this of the target
    [SerializeField] float riseHeight = 2f;    // unlocked: with no target, drifts up this far before it bursts
    [SerializeField] float fadeTime = 0.4f;    // locked: with no target, let the trail drift out this long, then despawn
    [SerializeField] float burstTime = 0.35f;  // glow bloom + fade, ~ the explosion length
    [SerializeField] float burstScale = 1.6f;  // glow scale multiplier at the burst peak
    [SerializeField] float hitPadding = 0.15f; // land a touch past the target's hit circle

    enum Phase { Spawning, Flying, Rising, Fading, Bursting }
    Phase _phase;
    float _t;
    float _riseStartY;
    float _burstScale;     // glow bloom target for the current burst (bigger when it self-destructs)
    Vector3 _glowScale;    // authored glow scale (the "current level" to grow into)
    Color _glowColor;

    Transform _caster;
    float _range, _damage, _knockback;
    int _team;
    bool _locked;          // true = home one mark + fade on loss (enemy); false = re-target + rise (player)
    IDamageable _target;
    bool _acquired;        // has the flight locked onto a target at least once
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

    public void Launch(Transform caster, float range, int team, float damage, float knockback)
    {
        _caster = caster;
        _range = range;
        _team = team;
        _damage = damage;
        _knockback = knockback;
        _locked = team != 1;   // the player's flame (team 1) re-targets + rises; anyone else's locks onto one mark + fades
        _target = null;
        _acquired = false;
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
            case Phase.Fading:   Fading(dt);   break;
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
        {
            // unlocked re-targets the nearest each time its mark is lost; locked picks once and never again.
            _target = (!_locked || !_acquired) ? FindNearest() : null;
            _acquired = true;
        }

        if (_target == null) { if (_locked) StartFade(); else StartRise(); return; }

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

        float v = speed * (slowRadius > 0f ? Mathf.Clamp(dist / slowRadius, 0.3f, 1f) : 1f);
        transform.position += (d / dist) * (v * dt);
    }

    // No target, unlocked: drift straight up, then burst harmlessly at the top (player).
    void StartRise()
    {
        _phase = Phase.Rising;
        _riseStartY = transform.position.y;
    }

    void Rising(float dt)
    {
        transform.position += Vector3.up * (speed * dt);
        if (transform.position.y - _riseStartY >= riseHeight) StartBurst(burstScale * 2f);   // wasted shot pops bigger
    }

    // No target, locked: stop dead — cut emission (no burst, no rise), let the last particles drift out over
    // fadeTime, then despawn.
    void StartFade()
    {
        _phase = Phase.Fading;
        _t = 0f;
        if (flame != null) flame.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    void Fading(float dt)
    {
        _t += dt;
        float k = fadeTime > 0f ? Mathf.Clamp01(_t / fadeTime) : 1f;
        if (glow != null) SetGlowAlpha(_glowColor.a * (1f - k));   // glow fades out with the trailing particles
        if (k >= 1f) LeanPool.Despawn(gameObject);
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
