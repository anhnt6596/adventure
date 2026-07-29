using UnityEngine;

// The four pluggable behaviours a monster composes. The FSM skeleton (EnemyAI) is shared and fixed; these are
// what make one monster differ from another.
//
// Implementations stay PLAIN C# — no ScriptableObject, no logic in assets. What lives in an asset is only the
// assembly table: an EnemyBrainConfig holds one chosen instance per slot via [SerializeReference], so picking
// which algorithm a monster uses (and tuning that algorithm's own numbers) is authoring, not code. Adding a new
// behaviour is one [Serializable] class implementing one of these — nothing else in the codebase has to learn
// about it, the inspector dropdown finds it by reflection.
//
// Rule for where a number lives: if the FSM skeleton reads it, it belongs on EnemyConfig (attackRange,
// leashRadius, forgetTime...). If exactly one behaviour reads it, it belongs on that behaviour.
public interface IIdleBehavior { void Tick(AIContext ctx); }                        // move while idle
public interface IAggro       { IDamageable Detect(AIContext ctx); }               // proactive target (null = none)
public interface IPursuit     { Vector2 DirTo(AIContext ctx, Vector3 targetPos); } // desired ground dir toward target
public interface IAttackPlan  { void Tick(AIContext ctx); }                        // when/how to fire (calls controller.Attack)
