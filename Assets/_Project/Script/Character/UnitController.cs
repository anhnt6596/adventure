using System;
using UnityEngine;

// Base for a controllable unit's movement + attack. The control surface a view/animator reads — Velocity,
// IsBusy, Attacked — lives here, so ONE view drives the player and an enemy alike. Whatever FEEDS Move stays
// external (player input, enemy AI). Subclasses supply the stat numbers from wherever they keep them —
// ICharacterStats for the player, EnemyConfig for an enemy — by overriding the accessors below.
public abstract class UnitController : Identifiable
{
    [SerializeField] protected CollisionBody body;   // mass comes from stats, not the inspector

    Vector2 _input;
    float _busyTimer;

    public bool IsBusy => _busyTimer > 0f;
    public Vector3 Velocity { get; private set; }
    public event Action Attacked;

    // The numbers the control loop needs; each unit kind sources them differently.
    protected abstract float MoveSpeed { get; }
    protected abstract float AttackSpeed { get; }
    protected abstract float AttackDuration { get; }
    protected abstract float Mass { get; }

    // Virtual so a unit whose stats aren't ready at Start (e.g. an enemy configured after spawn) can defer it.
    protected virtual void Start()
    {
        if (body == null)
        {
            Debug.LogError($"[{GetType().Name}] CollisionBody not assigned — no collision, no mass.", this);
            return;
        }
        body.SetMass(Mass);   // the body registers itself with CollisionSystem via OnEnable
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

        Velocity = new Vector3(move.x, 0f, move.y) * MoveSpeed;
        transform.position += Velocity * Time.deltaTime;
    }
}
