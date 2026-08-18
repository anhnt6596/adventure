using System.Collections.Generic;
using UnityEngine;
using Lean.Pool;

// A soul-fire projectile — same for player and enemy. It flies straight in the caster's facing direction up to
// Range, hitting the first hostile it touches (damage + burst) or bursting wide at the end if it hits nothing.
// As it travels it SEEKS: each frame it scans a cone ahead of its travel direction and bends toward the best
// target in there — so it never turns back on something it already flew past, and it prefers the OPPOSING combat
// team (a player shot, team 1, chases team 2; an enemy shot, team 2, chases team 1). A hit, unlike the seek, is
// direction-blind: anything touching the flame stops it. ONE prefab / particle for everyone. Pooled: every field
// is (re)set in Launch.
[DisallowMultipleComponent]
public class SoulFire : Projectile
{
    [Header("Refs")]
    [SerializeField] SpriteRenderer glow;      // the light; fades/scales in on spawn, blooms on burst
    [SerializeField] ParticleSystem flame;     // the trailing fire; runs through the flight
    [SerializeField] ParticleSystem burst;     // one-shot explosion on the end (set Play On Awake OFF)

    [Header("Timing")]
    [SerializeField] float spawnTime = 0.1f;   // glow fades + scales in over this
    [SerializeField] float burstTime = 0.35f;  // glow bloom + fade, ~ the explosion length
    [SerializeField] float burstScale = 1.6f;  // glow scale multiplier at the burst peak
    [SerializeField] float hitPadding = 0.15f; // contact reach past the target's hit circle

    [Header("Seek")]
    [SerializeField] float seekRange = 3f;     // how far ahead the cone reaches (≤ CombatWorld cell 8)
    [SerializeField, Range(0f, 180f)] float seekAngle = 60f;  // full cone opening around the travel direction
    [SerializeField] float steerRate = 180f;   // max turn deg/sec toward the scanned target — small = a gentle nudge, not homing

    enum Phase { Spawning, Flying, Bursting }
    Phase _phase;
    float _t;
    float _burstScale;     // glow bloom target for the current burst (bigger when it self-destructs)
    Vector3 _glowScale;    // authored glow scale (the "current level" to grow into)
    Color _glowColor;

    float _range, _speed, _damage, _knockback;
    Vector3 _dir;          // travel direction
    float _traveled;       // distance covered so far
    Component _source;     // the caster — passed as the damage source so a victim can hit back at the shooter
    readonly List<IDamageable> _found = new List<IDamageable>();

    // Already bursting, it is spent: there is nothing left to take out of the air, and cancelling it again
    // would restart the explosion under itself.
    protected override bool InFlight => _phase != Phase.Bursting;

    // The same reach it makes contact with, so what swats it out of the air is what it would have burnt.
    protected override bool Reaches(Vector3 point)
    {
        Vector3 d = point - transform.position;
        d.y = 0f;
        return d.sqrMagnitude <= hitPadding * hitPadding;
    }

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

    public override void Launch(in Shot shot)
    {
        _range = shot.Range;
        _speed = shot.Speed;
        _source = shot.Source;
        Team = shot.Team;
        _damage = shot.Damage;
        _knockback = shot.Knockback;
        _dir = shot.Direction;
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
        Scan(out var contact, out var target);

        if (contact != null)   // touching — deal the hit (always, from the muzzle on, from any direction)
        {
            Vector3 to = contact.Position - transform.position; to.y = 0f;
            float dist = to.magnitude;
            contact.TakeDamage(_damage, _source != null ? (object)_source : this);
            if (_knockback > 0f && dist > 1e-4f) contact.ApplyKnockback((to / dist) * _knockback);
            StartBurst(burstScale);
            return;
        }
        if (target != null)   // something ahead in the cone — bend toward it
        {
            Vector3 to = target.Position - transform.position; to.y = 0f;
            _dir = Vector3.RotateTowards(_dir, to.normalized, steerRate * Mathf.Deg2Rad * dt, 0f);
        }

        float step = _speed * dt;
        transform.position += _dir * step;
        _traveled += step;
        if (_traveled >= _range) StartBurst(burstScale * 2f);   // ran the full range, hit nothing — wide burst
    }

    // Swatted out of the air. It BURSTS rather than blinking off, because that is what this flame does when it
    // stops — being met by a blade is a way of stopping, not a way of never having been there. Despawning would
    // also cut the flame particles mid-emit and leave a hole where the player was watching.
    //
    // The narrow burst, the one it makes on a body: it was stopped by something, not spent on empty ground.
    // NO DAMAGE with it — cancelling is what the blade earned, and a flame that still burnt whatever knocked it
    // down would make blocking a worse answer than dodging.
    protected override void Cancel() => StartBurst(burstScale);

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

    // One sweep of the hostiles around the flame (Overlap filters own team + alive), split into the two things
    // the shot cares about: `contact` is anything already touching it, whatever the direction — a hit can't be
    // dodged by standing off to the side; `target` is the best hostile inside the cone ahead, the one to steer at.
    void Scan(out IDamageable contact, out IDamageable target)
    {
        contact = null;
        target = null;

        Vector3 from = transform.position;
        CombatWorld.Instance.Rebuild();
        CombatWorld.Instance.Overlap(from, Mathf.Max(hitPadding, seekRange), Team, _found);

        float minDot = Mathf.Cos(seekAngle * 0.5f * Mathf.Deg2Rad);
        float contactSq = float.MaxValue, targetSq = float.MaxValue;
        bool contactPriority = false, targetPriority = false;

        for (int i = 0; i < _found.Count; i++)
        {
            var c = _found[i];
            // Creatures first, scenery second — a shot will still burn a tree, but never in preference to the
            // thing fighting you. This used to name the one opposing team outright (1 chases 2, 2 chases 1);
            // with a team per monster kind there is no single opponent left to name.
            bool priority = Teams.IsPrey(c.Team);
            Vector3 d = c.Position - from; d.y = 0f;
            float sq = d.x * d.x + d.z * d.z;

            float touch = c.HitRadius + hitPadding;
            if (sq <= touch * touch && Better(priority, sq, contact, contactPriority, contactSq))
            {
                contact = c;
                contactPriority = priority;
                contactSq = sq;
            }
            // The cone test needs a direction, so it skips anything sitting on the flame — that one is a
            // contact anyway, and the hit above already claimed it.
            if (sq > 1e-8f && sq <= seekRange * seekRange
                && Vector3.Dot(_dir, d / Mathf.Sqrt(sq)) >= minDot
                && Better(priority, sq, target, targetPriority, targetSq))
            {
                target = c;
                targetPriority = priority;
                targetSq = sq;
            }
        }
    }

    // A priority-team target beats any non-priority one; within the same class, the nearer wins — so the shot
    // chases an enemy over a bystander, and the closest of those.
    static bool Better(bool priority, float sq, IDamageable best, bool bestPriority, float bestSq)
        => best == null || (priority && !bestPriority) || (priority == bestPriority && sq < bestSq);

    void SetGlowAlpha(float a)
    {
        var c = _glowColor;
        c.a = a;
        glow.color = c;
    }
}
