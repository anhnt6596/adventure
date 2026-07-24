using System;
using UnityEngine;

// Base for a controllable unit's movement + attack. The control surface a view/animator reads — Velocity,
// IsBusy, Attacked — lives here, so ONE view drives the player and an enemy alike. Whatever FEEDS Move stays
// external (player input, enemy AI). Subclasses supply the stat numbers from wherever they keep them —
// ICharacterStats for the player, EnemyConfig for an enemy — by overriding the accessors below.
public abstract class UnitController : Identifiable
{
    protected CollisionBody body;   // the unit's body, auto-found under it — not wired by hand

    Vector2 _input;
    float _busyTimer;

    public bool IsBusy => _busyTimer > 0f;
    public Vector3 Velocity { get; private set; }
    public event Action Attacked;

    // Which way the unit is turned in the WORLD, as an 8-sector index (ViewAngleUtil, clockwise from +Z):
    // its last move direction, held while idle. A view turns this into a screen-relative direction against
    // the camera, so the sprite re-aims when the camera orbits even while the unit stands still.
    public int Facing { get; private set; }

    // 0 neutral / 1 player / 2 enemy. A kind sets its side here (MC = 1, Enemy = 2) and its attacks read it,
    // so the same attack component fights for whoever owns it.
    public virtual int Team => 0;

    // The numbers the control loop needs; each unit kind sources them differently.
    protected abstract float MoveSpeed { get; }
    protected abstract float AttackSpeed { get; }
    protected abstract float AttackDuration { get; }
    protected abstract float Mass { get; }

    // Virtual so a unit whose stats aren't ready at Start (e.g. an enemy configured after spawn) can defer it.
    protected virtual void Start()
    {
        body = GetComponentInChildren<CollisionBody>();
        if (body == null)
        {
            Debug.LogError($"[{GetType().Name}] no CollisionBody found — no collision, no mass.", this);
            return;
        }
        // TEMP: mass is set once from base stats. Later it becomes dynamic (gear, upgrades, buffs) and should
        // be recomputed on change, not this one-shot at Start. (The body registers itself via its OnEnable.)
        body.SetMass(Mass);
    }

    public void Move(Vector2 worldDir)
    {
        if (IsBusy) return;
        _input += worldDir;
    }

    public void Attack()
    {
        if (IsBusy) return;
        _busyTimer = AttackSpeed > 0f ? AttackDuration / AttackSpeed : AttackDuration;
        Attacked?.Invoke();
    }

    protected virtual void Update()
    {
        if (_busyTimer > 0f) _busyTimer -= Time.deltaTime;

        var move = Vector2.ClampMagnitude(_input, 1f);
        _input = Vector2.zero;

        // While a knockback shove is carrying the body, it drives movement — don't fight it with input.
        if (body != null && body.IsKnocked) { Velocity = Vector3.zero; return; }

        if (move.sqrMagnitude > 0.0001f)
            Facing = ViewAngleUtil.GetViewType8(Mathf.Atan2(move.x, move.y) * Mathf.Rad2Deg);

        Velocity = new Vector3(move.x, 0f, move.y) * MoveSpeed;
        transform.position += Velocity * Time.deltaTime;
    }
}
