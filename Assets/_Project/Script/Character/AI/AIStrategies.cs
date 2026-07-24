using UnityEngine;

// The four pluggable behaviours a monster composes. The FSM skeleton (EnemyAI) is shared and fixed; these are
// what make one monster differ from another. Plain C# — a monster picks implementations in code (see MewFrogAI).
public interface IIdleBehavior { void Tick(AIContext ctx); }                        // move while idle
public interface IAggro       { IDamageable Detect(AIContext ctx); }               // proactive target (null = none)
public interface IPursuit     { Vector2 DirTo(AIContext ctx, Vector3 targetPos); } // desired ground dir toward target
public interface IAttackPlan  { void Tick(AIContext ctx); }                        // when/how to fire (calls controller.Attack)

// The set a monster hands EnemyAI. A plain bundle — no logic, just the chosen strategies.
public struct AIStrategies
{
    public IIdleBehavior Idle;
    public IAggro Aggro;
    public IPursuit Pursuit;
    public IAttackPlan Attack;
}
