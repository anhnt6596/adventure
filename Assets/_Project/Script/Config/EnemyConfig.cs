using UnityEngine;

[CreateAssetMenu(menuName = "Config/Enemy")]
public class EnemyConfig : Config, IDamageableConfig
{
    // Which side this KIND is on — one team per kind, not one team for "enemies". That is what lets a predator
    // hunt a critter and two monsters maul each other; a shared enemy team made every one of them allies by
    // accident. Teams.Critter (2) for the peaceful ones, Teams.FirstMonster (3) and up for the rest, a distinct
    // number each. Never Teams.Resource — that is scenery, and no AI will hunt it.
    [Tooltip("1 = player · 2 = peaceful critter (never starts a fight) · 3, 4, 5... = one per aggressive kind, "
           + "a distinct number each so they fight each other · 10000+ = resources, which no AI will ever hunt.")]
    public int team = Teams.FirstMonster;

    public float hp = 10f;
    public float moveSpeed = 2f;
    public float attackDamage = 1f;
    public float attackSpeed = 1f;
    public float attackCooldown = 0f;   // recovery AFTER the swing ends, at 1x attackSpeed; one attack costs swing + this (0 = swing back-to-back)

    // How heavy it is, and it means two things at once because CollisionBody uses it for both: knockback scales
    // by 1/mass (1 = full, 2 = half, 0.5 = double, 0 = rooted to the spot), and so does being shoved aside when
    // bodies overlap. It has to live HERE and not on the prefab's CollisionBody: DynamicUnit.Start pushes this
    // number down with SetMass, overwriting whatever the prefab said — so a mass authored over there is silently
    // thrown away. Same reason MC carries its mass in MainCharStatsConfig.
    public float mass = 1f;
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
    public int Team => team;
}
