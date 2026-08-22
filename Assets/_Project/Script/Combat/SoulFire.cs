using System.Collections.Generic;
using UnityEngine;
using Lean.Pool;

// A soul-fire projectile — same for player and enemy. It flies STRAIGHT along the direction it was launched in,
// up to Range, hitting the first hostile it touches (damage + burst) or bursting wide at the end if it hit
// nothing. The hit is direction-blind: anything touching the flame stops it, from any side. ONE prefab /
// particle for everyone. Pooled: every field is (re)set in Launch.
//
// IT DOES NOT SEEK. It used to bend toward whatever stood in a cone ahead of it, and that made the shot the
// thing doing the aiming — a flame you could not dodge by moving, only by breaking line of sight it never had.
// Where it goes is now settled entirely at the moment it is let go, which is what makes stepping out of the way
// an answer. Nothing was left behind to turn it back on: a shot that chases is a different weapon, not a
// setting on this one.
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
        var contact = Contact();
        if (contact != null)   // touching — deal the hit (always, from the muzzle on, from any direction)
        {
            Vector3 to = contact.Position - transform.position; to.y = 0f;
            float dist = to.magnitude;
            contact.TakeDamage(_damage, _source != null ? (object)_source : this);
            if (_knockback > 0f && dist > 1e-4f) contact.ApplyKnockback((to / dist) * _knockback);
            StartBurst(burstScale);
            return;
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

    // What the flame is TOUCHING right now, or null. Direction-blind on purpose: a flame is not a blade, and
    // standing off to one side of the thing you are standing on is not a way of not being burnt by it.
    //
    // Overlap already IS the contact test — it measures each body's own hit circle against the radius it is
    // handed — so the padding goes in and the answer comes back, with nothing left here to re-measure.
    IDamageable Contact()
    {
        Vector3 from = transform.position;
        CombatWorld.Instance.Rebuild();
        CombatWorld.Instance.Overlap(from, hitPadding, Team, _found);

        IDamageable best = null;
        bool bestPriority = false;
        float bestSq = float.MaxValue;

        for (int i = 0; i < _found.Count; i++)
        {
            var c = _found[i];
            // Creatures first, scenery second — the flame will still burn a tree, but never in preference to
            // the thing fighting you. Two bodies shoulder to shoulder is one contact, and which of them the
            // query happened to return first is not something the player could see or aim at.
            bool priority = Teams.IsPrey(c.Team);
            Vector3 d = c.Position - from; d.y = 0f;
            float sq = d.x * d.x + d.z * d.z;

            if (!Better(priority, sq, best, bestPriority, bestSq)) continue;
            best = c;
            bestPriority = priority;
            bestSq = sq;
        }
        return best;
    }

    // A priority-team body beats any non-priority one; within the same class, the nearer wins — so the flame
    // spends itself on a creature over a bystander, and on the closest of those.
    static bool Better(bool priority, float sq, IDamageable best, bool bestPriority, float bestSq)
        => best == null || (priority && !bestPriority) || (priority == bestPriority && sq < bestSq);

    void SetGlowAlpha(float a)
    {
        var c = _glowColor;
        c.a = a;
        glow.color = c;
    }
}
