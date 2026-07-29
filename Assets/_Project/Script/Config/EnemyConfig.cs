using UnityEngine;

[CreateAssetMenu(menuName = "Config/Enemy")]
public class EnemyConfig : Config, IDamageableConfig
{
    public float hp = 10f;
    public float moveSpeed = 2f;
    public float attackDamage = 1f;
    public float attackSpeed = 1f;
    public float attackDuration = 2f;   // lock while the swing plays (can't move or attack), at 1x attackSpeed — match it to the attack clip's length
    public float attackCooldown = 0f;   // seconds between attack starts, at 1x attackSpeed; floored at attackDuration (0 = swing back-to-back)
    public float hitRadius = 0.5f;   // body size for being hit (defensive). Attack reach is a different thing — it stays on the attack component.
    // attack range is spatial → it lives on the view/attack component (like ShapeAttack.radius), not here

    [Header("AI")]
    public float attackRange = 3f;      // brain: stop and fire within this of the target (projectile homes with no range of its own)
    public float aggroRadius = 4f;      // proactive sight range (SightAggro); keep ≤ CombatWorld hash cell (8)
    public float leashRadius = 8f;      // give up the chase past this distance
    public float reEngageRadius = 5f;   // in Forget, resume if the target comes back within this
    public float forgetTime = 3f;       // seconds standing still before returning to idle
    public float wanderRadius = 3f;     // idle amble bound around the spawn point
    public float recognizeTime = 1f;    // reaction delay: on first turning aggressive (spotted / got hit), freeze in Idle this long before engaging

    // IDamageableConfig — so EnemySpawner can bind this straight onto the enemy's Damageable at spawn, the
    // same numbers a placed thing would carry as a PropConfig. Team is a fallback: Damageable takes
    // the unit's team when there is one (an enemy always has one), so this value rarely wins.
    public float MaxHp => hp;
    public float HitRadius => hitRadius;
    public int Team => 2;
}
