using System.Collections.Generic;
using UnityEngine;
using Lean.Pool;

// A thrown knife: straight, no seeking, until it runs out of range or hits something.
//
// ONE BLOW AND IT IS GONE, whatever that blow did. It does not carve through what it kills, which makes the
// fan the answer to a crowd rather than aiming one knife down a queue — three knives are three hits and never
// more, so what the skill is worth can be read off the count and does not swing on how the enemies lined up.
//
// A CIRCLE, and it is the tumble that earns it: a blade spinning end over end has no long side for more than
// an instant, so an oriented box would be claiming a precision the drawing does not show. One radius is also
// one number to tune instead of two that have to be judged against each other.
//
// Pooled: every field is (re)set in Launch, the hit list included.
[DisallowMultipleComponent]
public class Knife : Projectile
{
    [Tooltip("The blade's reach, as a circle around it. Not the sprite's size — this is what it catches on, " +
             "and it is usually a little smaller than what is drawn.")]
    [SerializeField, Min(0.01f)] float radius = 0.3f;

    [Tooltip("The child holding the sprite. Only this tumbles, and it is purely the look — what the blade " +
             "catches on is a circle, so nothing it hits depends on which way the drawing is pointing.")]
    [SerializeField] Transform art;

    [Tooltip("Degrees per second the blade tumbles in the air, about Y — the blade lies flat, so it pinwheels " +
             "over the ground rather than tipping up out of it. Sign is the direction; 0 = no tumble.")]
    [SerializeField] float spin = -720f;

    Vector3 _dir;
    float _speed, _range, _damage, _knockback, _traveled;
    int _team;
    Component _source;

    // The tilt the blade was AUTHORED with — how it lies on the ground, decided once in the prefab and never
    // a runtime concern. Cached before anything has flown, because from then on the live value is this plus a
    // spin, and re-reading it would compound.
    Quaternion _artRest;
    float _spun;      // degrees of yaw laid over that rest pose so far this throw

    // No memory of who has been hit: the knife dies on the contact, so there is never a second one to check
    // against. That was needed while it could fly on through a kill.
    readonly List<IDamageable> _found = new List<IDamageable>();

    void Awake()
    {
        if (art != null) _artRest = art.localRotation;
    }

    public override void Launch(in Shot shot)
    {
        _dir = shot.Direction;
        _speed = shot.Speed;
        _range = shot.Range;
        _damage = shot.Damage;
        _knockback = shot.Knockback;
        _team = shot.Team;
        _source = shot.Source;
        _traveled = 0f;

        // THE ROOT IS NOT TURNED TO FACE THE THROW, and nothing else may turn it either. What the knife
        // catches on is a circle, so pointing it costs a frame and buys nothing — while a root swung round to
        // the flight direction would drag the blade's authored lie with it, and the same knife would land flat
        // going one way and on its edge going another.
        //
        // Back to the pose the prefab was built in. This object has flown before, so without the reset every
        // throw would start at whatever angle the last one stopped at.
        _spun = 0f;
        if (art != null) art.localRotation = _artRest;
    }

    void Update()
    {
        // YAW ONLY, AND X AND Z SURVIVE IT UNTOUCHED. Unity composes euler angles as Y·X·Z, so a yaw applied
        // on the LEFT of the rest pose comes out as exactly (x, y + spun, z) — the authored lie of the blade
        // is carried through every frame of the spin rather than being ground away by it.
        //
        // Turning the transform in place instead would spin it about the blade's OWN up, and that axis is
        // itself tilted by the authored X — which is what was rolling the knife over as it flew.
        if (art != null && spin != 0f)
        {
            _spun += spin * Time.deltaTime;
            art.localRotation = Quaternion.Euler(0f, _spun, 0f) * _artRest;
        }

        CombatWorld.Instance.Rebuild();   // once for the whole move, however many hops it takes

        // WALKED, NOT JUMPED. The knife covers the frame in hops no longer than it is wide, testing where it
        // lands each time, so a fast one cannot step clean over a target between two frames. Testing only where
        // it ended up would make a hit depend on the frame rate, which is the one thing a hit must never do.
        //
        // The last move is CUT to whatever is left of the range, so the knife stops exactly where its reach
        // ends rather than at the end of the hop that crossed it. Without the cut the overshoot is up to one
        // hop, and a hop is as long as the knife is fast — which would quietly make a faster knife a
        // longer-ranged one, the one thing speed must not buy.
        float step = _speed * Time.deltaTime;
        bool spent = step >= _range - _traveled;
        if (spent) step = _range - _traveled;

        int hops = Mathf.Max(1, Mathf.CeilToInt(step / radius));
        float hop = step / hops;

        for (int i = 0; i < hops; i++)
        {
            transform.position += _dir * hop;
            _traveled += hop;

            if (Bite()) return;                        // caught by something that lived through it
        }

        // The far end still gets its blow first: the hop above ran, and Bite tested where it landed, so
        // something standing exactly on the limit is hit rather than watched from a knife already gone.
        if (spent) LeanPool.Despawn(gameObject);
    }

    // Everything the blade is touching where it now stands. Returns true if the knife is spent.
    bool Bite()
    {
        CombatWorld.Instance.Overlap(transform.position, radius, _team, _found);
        if (_found.Count == 0) return false;

        // EVERYTHING IN THE CIRCLE, then gone. It stops on contact whatever the blow did, but it stops at the
        // END of the circle it is standing in rather than on the first name out of the list — two things
        // shoulder to shoulder are one contact, and which of them the query happened to return first is not
        // something the player can see or aim at.
        for (int i = 0; i < _found.Count; i++)
        {
            var victim = _found[i];
            victim.TakeDamage(_damage, _source != null ? (object)_source : this);
            if (_knockback > 0f) victim.ApplyKnockback(_dir * _knockback);
        }

        LeanPool.Despawn(gameObject);
        return true;
    }

    // On the ground plane, not facing the scene camera: what it catches is a circle laid flat over the world,
    // and a gizmo tilted to the view would read as a reach it does not have.
    void OnDrawGizmosSelected()
    {
        const int segments = 28;
        Vector3 c = transform.position;
        Vector3 prev = c + new Vector3(radius, 0f, 0f);

        Gizmos.color = new Color(1f, 0.4f, 0.3f, 0.6f);
        for (int i = 1; i <= segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            Vector3 next = c + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}
