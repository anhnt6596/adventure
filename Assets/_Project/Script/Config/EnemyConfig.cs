using UnityEngine;

[CreateAssetMenu(menuName = "Config/Enemy")]
public class EnemyConfig : Config
{
    public float hp = 10f;
    public float moveSpeed = 2f;
    public float attackDamage = 1f;
    public float attackSpeed = 1f;
    public float attackRange = 1f;
}
