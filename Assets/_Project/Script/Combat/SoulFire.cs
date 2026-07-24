using System.Collections.Generic;
using UnityEngine;
using Lean.Pool;

// A soul-fire projectile with two flight modes, chosen by the caster's TEAM:
//   Player (team 1) - a straight shot in the caster's facing direction; travels up to Range, hits the first
//                     hostile it touches (damage + burst), or bursts wide at the end of its range if it hits none.
//   Enemy (else)    - a slow homing flame that locks its first target and chases it to the end; if that mark
//                     dies or vanishes, it stops and fades out (no burst).
// Phases: Spawning (glow fades/scales in) -> Flying (straight or homing) -> Bursting / Fading -> despawn.
// ONE prefab / particle for both. Pooled: every field is (re)set in Launch, so a recycled flame is clean.
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
    [SerializeField] float slowRadius = 1.5f;  // homing only: eases the drift down within this of the target
    [SerializeField] float fadeTime = 0.4f;    // enemy: with no target, let the trail drift out this long, then despawn
    [SerializeField] float burstTime = 0.35f;  // glow bloom + fade, ~ the explosion length
    [SerializeField] float burstScale = 1.6f;  // glow scale multiplier at the burst peak
    [SerializeField] float hitPadding = 0.15f; // contact reach past the target's hit circle

    [Header("Player shot")]
    [SerializeField] float seekRadius = 1.5f;  // detect an enemy within this of the flame to steer toward (≤ CombatWorld cell 8)
    [SerializeField] float steerRate = 180f;   // max turn toward it, deg/sec — small = a gentle nudge, not homing

    enum Phase { Spawning, Flying, Fading, Bursting }
    Phase _phase;
    float _t;
    float _burstScale;     // glow bloom target for the current burst (bigger when it self-destructs)
    Vector3 _glowScale;    // authored glow scale (the "current level" to grow into)
    Color _glowColor;

    Transform _caster;
    float _range, _damage, _knockback;
    int _team;
    bool _locked;          // team != 1 -> homing enemy flame; team 1 -> straight player shot
    Vector3 _dir;          // player shot: travel direction
    float _traveled;       // player shot: distance covered so far
    IDamageable _target;   // enemy: the locked mark
    bool _acquired;        // enemy: has it locked once
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

    public void Launch(Transform caster, float range, int team, float damage, float knockback, Vector3 direction)
    {
        _caster = caster;
        _range = range;
        _team = team;
        _damage = damage;
        _knockback = knockback;
        _locked = team != 1;
        _dir = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.forward;
        _traveled = 0f;
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
            case Phase.Flying:   if (_locked) FlyHoming(dt); else FlyStraight(dt); break;
            case Phase.Fading:   Fading(dt); break;
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

    // Player shot: flies along _dir, but if an enemy is within seekRadius it bends a little toward it (a gentle
    // nudge, not homing — the turn is capped by steerRate). Hits on contact -> burst; runs the full Range
    // hitting nothing -> wide burst.
    void FlyStraight(float dt)
    {
        var near = NearestHostile(transform.position, seekRadius);
        if (near != null)
        {
            Vector3 to = near.Position - transform.position; to.y = 0f;
            float dist = to.magnitude;
            if (dist <= near.HitRadius + hitPadding)   // touching — deal the hit
            {
                near.TakeDamage(_damage, this);
                if (_knockback > 0f && dist > 1e-4f) near.ApplyKnockback((to / dist) * _knockback);
                StartBurst(burstScale);
                return;
            }
            if (dist > 1e-4f)   // in range but not touching — steer a touch toward it
                _dir = Vector3.RotateTowards(_dir, to / dist, steerRate * Mathf.Deg2Rad * dt, 0f);
        }

        float step = speed * dt;
        transform.position += _dir * step;
        _traveled += step;
        if (_traveled >= _range) StartBurst(burstScale * 2f);   // ran the full range, hit nothing — wide burst
    }

    // Enemy flame: lock the nearest at launch and home it; fade out if it's lost.
    void FlyHoming(float dt)
    {
        if (_target == null || !_target.IsAlive)
        {
            _target = _acquired ? null : NearestHostile(_caster != null ? _caster.position : transform.position, _range);   // locked: acquire once
            _acquired = true;
        }

        if (_target == null) { StartFade(); return; }

        Vector3 d = _target.Position - transform.position;
        d.y = 0f;
        float dist = d.magnitude;

        if (dist <= _target.HitRadius + hitPadding)
        {
            _target.TakeDamage(_damage, this);
            if (_knockback > 0f && dist > 1e-4f)
                _target.ApplyKnockback((d / dist) * _knockback);
            StartBurst(burstScale);
            return;
        }

        float v = speed * (slowRadius > 0f ? Mathf.Clamp(dist / slowRadius, 0.3f, 1f) : 1f);
        transform.position += (d / dist) * (v * dt);
    }

    // Stop dead: cut emission (no burst), let the last particles drift out over fadeTime, then despawn (enemy).
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

    // Nearest live hostile within radius of a centre (Overlap filters team + alive), ranked by distance from the
    // flame itself — so the straight shot steers toward whatever's closest to it.
    IDamageable NearestHostile(Vector3 centre, float radius)
    {
        CombatWorld.Instance.Rebuild();
        CombatWorld.Instance.Overlap(centre, radius, _team, _found);
        return Nearest(transform.position);
    }

    IDamageable Nearest(Vector3 from)
    {
        IDamageable best = null;
        float bestSq = float.MaxValue;
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
