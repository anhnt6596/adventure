using UnityEngine;

public class CharacterStats : MonoBehaviour, ICharacterStats
{
    [SerializeField] float baseMoveSpeed = 6f;
    [SerializeField] float baseAttackSpeed = 1f;

    public Stat MoveSpeed { get; private set; }
    public Stat AttackSpeed { get; private set; }

    void Awake()
    {
        MoveSpeed = new Stat(baseMoveSpeed);
        AttackSpeed = new Stat(baseAttackSpeed);
    }
}
