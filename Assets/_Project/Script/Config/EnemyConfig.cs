using UnityEngine;

[CreateAssetMenu(menuName = "Config/Enemy")]
public class EnemyConfig : Config, IDamageableConfig
{
    public float hp = 10f;
    public float moveSpeed = 2f;
    public float attackDamage = 1f;
    public float attackSpeed = 1f;
    public float attackCooldown = 0f;   // recovery AFTER the swing ends, at 1x attackSpeed; one attack costs swing + this (0 = swing back-to-back)
    // Nothing SPATIAL lives here. A size or a reach has to be judged against the ART, so it is authored where
    // the art is — hit radius on the Damageable, attack reach on the attack component (ShapeAttack.radius) —
    // each with a gizmo you can see it against. Attack RANGE is the odd one out: it's a decision ("how close do
    // I get before I stop"), not a size, so it lives in the brain.
    //
    // What's left is the BODY: what the unit IS. How it THINKS is a whole asset of its own — behaviours, their
    // tuning, and the FSM's distances/timers all live in the brain. Nothing about AI here but the pointer, so
    // two kinds can share one mind, or carry identical stats and think nothing alike.
    [Header("AI")]
    public EnemyBrainConfig brain;

    // IDamageableConfig — so EnemySpawner can bind this straight onto the enemy's Damageable at spawn, the
    // same numbers a placed thing would carry as a PropConfig. Team is a fallback: Damageable takes
    // the unit's team when there is one (an enemy always has one), so this value rarely wins.
    public float MaxHp => hp;
    public int Team => 2;
}
