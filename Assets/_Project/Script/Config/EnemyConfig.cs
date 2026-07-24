using UnityEngine;

[CreateAssetMenu(menuName = "Config/Enemy")]
public class EnemyConfig : Config, IDamageableConfig
{
    public float hp = 10f;
    public float moveSpeed = 2f;
    public float attackDamage = 1f;
    public float attackSpeed = 1f;
    public float hitRadius = 0.5f;   // body size for being hit (defensive). Attack reach is a different thing — it stays on the attack component.
    // attack range is spatial → it lives on the view/attack component (like SwingAttack.radius), not here

    // IDamageableConfig — so EnemySpawner can bind this straight onto the enemy's Damageable at spawn, the
    // same numbers a placed thing would drag in as a DamageableConfig. Team is a fallback: Damageable takes
    // the UnitController's team when there is one (an enemy always has one), so this value rarely wins.
    public float MaxHp => hp;
    public float HitRadius => hitRadius;
    public int Team => 2;
}
