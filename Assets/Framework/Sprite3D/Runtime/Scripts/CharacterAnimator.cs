using System;
using UnityEngine;

public class CharacterAnimator : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Transform scaleNode;
    [SerializeField] private Dir dirType = Dir.Dir2;
    public enum Dir { Dir2 = 0, Dir4 = 1, Dir8 = 2 }
    private int curDir;
    private bool isFlip;
    private Vector3 oriScale;

    private void Awake()
    {
        oriScale = scaleNode.localScale;
    }

    // 0: idle, 1: move, 2: other
    public int State => animator.GetInteger("State");
    public void UpdateState(int state)
    {
        if (animator.GetInteger("State") != state) animator.SetInteger("State", state);
    }

    public void UpdateDir(int dir)
    {
        int notFlipDir;
        (notFlipDir, isFlip) = CalculateDir(dir);
        if (animator.GetInteger("Dir") != notFlipDir) animator.SetInteger("Dir", dir);
        scaleNode.localScale = new Vector3(oriScale.x * (isFlip ? -1 : 1), oriScale.y, oriScale.z);
    }

    public void TriggerAttack()
    {
        UpdateState(2);
        animator.SetTrigger("Attack");
    }

    private (int curDir, bool isFlip) CalculateDir(int dir)
    {
        switch (dirType)
        {
            case Dir.Dir2:
                return (1, dir != 1);
            case Dir.Dir4:
                return (dir == 3 ? 1 : dir, dir == 3);
        }
        return default;
    }
}
