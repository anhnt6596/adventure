using UnityEngine;

[CreateAssetMenu(menuName = "Stats/Main Char Stats")]
public class MainCharStatsConfig : ScriptableObject
{
    public float moveSpeed = 6f;
    public float attackSpeed = 1f;
    public float attackPower = 5f;
    public float attackDuration = 0.4f;
    public float mass = 1f;             // how hard to shove aside in collisions (not physics)
    public float pickupRadius = 1.5f;   // how close a pickable must be for the character to grab it
}
