using System.Collections.Generic;
using UnityEngine;
using Lean.Pool;

// A soul-fire projectile — same for player and enemy. It flies straight in the caster's facing direction up to
// Range, hitting the first hostile it touches (damage + burst) or bursting wide at the end if it hits nothing.
// As it travels it SEEKS: its reach + steer strength ramp up (to their max by rampUpBy of Range), bending it
// toward a nearby target — and it prefers the OPPOSING combat team (a player shot, team 1, chases team 2; an
// enemy shot, team 2, chases team 1). ONE prefab / particle for everyone. Pooled: every field is (re)set in Launch.
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
    [SerializeField] float burstTime = 0.35f;  // glow bloom + fade, ~ the explosion length
    [SerializeField] float burstScale = 1.6f;  // glow scale multiplier at the burst peak
    [SerializeField] float hitPadding = 0.15f; // contact reach past the target's hit circle

    [Header("Seek")]
    [SerializeField] float seekRadius = 1.5f;  // seek reach at full ramp (≤ CombatWorld cell 8)
    [SerializeField] float steerRate = 180f;   // max turn deg/sec at full ramp — small = a gentle nudge, not homing
    [SerializeField, Range(0f, 1f)] float rampUpBy = 0.5f;   // reach + steer hit their max by this fraction of Range flown, then hold

    enum Phase { Spawning, Flying, Bursting }
    Phase _phase;
    float _t;
    float _burstScale;     // glow bloom target for the current burst (bigger when it self-destructs)
    Vector3 _glowScale;    // authored glow scale (the "current level" to grow into)
    Color _glowColor;

    float _range, _damage, _knockback;
    int _team;             // caster's team — Overlap spares it (no friendly fire / no self-seek)
    int _priorityTeam;     // the team this shot chases first (the opposing combatant)
    Vector3 _dir;          // travel direction
    float _traveled;       // distance covered so far
    Component _source;     // the caster — passed as the damage source so a victim can hit back at the shooter
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

    public void Launch(float range, int team, float damage, float knockback, Vector3 direction, Component source)
    {
        _range = range;
        _source = source;
        _team = team;
        _priorityTeam = team == 1 ? 2 : team == 2 ? 1 : 0;   // chase the opposing combat team first
        _damage = damage;
        _knockback = knockback;
        _dir = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.forward;
        _traveled = 0f;
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
        // Seek reach + steer strength ramp with distance flown (t, maxing out by rampUpBy of Range): near the
        // muzzle it flies almost straight, farther out it reaches wider and turns harder toward a target.
        float rampDist = _range * rampUpBy;
        float t = rampDist > 0f ? Mathf.Clamp01(_traveled / rampDist) : 1f;
        float reach = seekRadius * t;

        var near = NearestHostile(transform.position, Mathf.Max(hitPadding, reach));
        if (near != null)
        {
            Vector3 to = near.Position - transform.position; to.y = 0f;
            float dist = to.magnitude;
            if (dist <= near.HitRadius + hitPadding)   // touching — deal the hit (always, from the muzzle on)
            {
                near.TakeDamage(_damage, _source != null ? (object)_source : this);
                if (_knockback > 0f && dist > 1e-4f) near.ApplyKnockback((to / dist) * _knockback);
                StartBurst(burstScale);
                return;
            }
            if (dist <= reach && dist > 1e-4f)   // within the growing seek reach — steer with the growing rate
                _dir = Vector3.RotateTowards(_dir, to / dist, steerRate * t * Mathf.Deg2Rad * dt, 0f);
        }

        float step = speed * dt;
        transform.position += _dir * step;
        _traveled += step;
        if (_traveled >= _range) StartBurst(burstScale * 2f);   // ran the full range, hit nothing — wide burst
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

    // Best hostile within radius of a centre (Overlap filters own team + alive). Prefers the priority team, then
    // the nearest — so the shot chases an enemy over a bystander, and the closest of those.
    IDamageable NearestHostile(Vector3 centre, float radius)
    {
        CombatWorld.Instance.Rebuild();
        CombatWorld.Instance.Overlap(centre, radius, _team, _found);

        IDamageable best = null;
        float bestSq = float.MaxValue;
        bool bestPriority = false;
        Vector3 from = transform.position;
        for (int i = 0; i < _found.Count; i++)
        {
            var c = _found[i];
            bool priority = c.Team == _priorityTeam;
            Vector3 d = c.Position - from; d.y = 0f;
            float sq = d.x * d.x + d.z * d.z;
            // a priority-team target beats any non-priority one; within the same class, prefer the nearer.
            if (best == null || (priority && !bestPriority) || (priority == bestPriority && sq < bestSq))
            {
                best = c;
                bestSq = sq;
                bestPriority = priority;
            }
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
