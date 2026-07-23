using System;
using UnityEngine;
using VContainer;

public class Character : Identifiable
{
    [SerializeField] CollisionBody body;   // the character's body; its mass comes from stats, not the inspector

    ICharacterStats _stats;
    CollisionSystem _collision;
    Vector2 _input;
    float _busyTimer;

    public bool IsBusy => _busyTimer > 0f;
    public Vector3 Velocity { get; private set; }
    public event Action Attacked;

    [Inject]
    public void Construct(ICharacterStats stats, CollisionSystem collision)
    {
        _stats = stats;
        _collision = collision;
    }

    void Start()
    {
        if (body == null)
        {
            Debug.LogError($"[{nameof(Character)}] CollisionBody not assigned — no collision, no mass.", this);
            return;
        }

        body.SetMass(_stats.Mass);
        body.BindSystem(_collision);
    }

    public void Move(Vector2 worldDir)
    {
        if (IsBusy) return;
        _input += worldDir;
    }

    public void Attack()
    {
        if (IsBusy) return;
        var atkSpeed = _stats.AttackSpeed.Value;
        var dur = _stats.AttackDuration;
        _busyTimer = atkSpeed > 0f ? dur / atkSpeed : dur;
        Attacked?.Invoke();
    }

    void Update()
    {
        if (_busyTimer > 0f) _busyTimer -= Time.deltaTime;

        var move = Vector2.ClampMagnitude(_input, 1f);
        _input = Vector2.zero;

        Velocity = new Vector3(move.x, 0f, move.y) * _stats.MoveSpeed.Value;
        transform.position += Velocity * Time.deltaTime;
    }
}
