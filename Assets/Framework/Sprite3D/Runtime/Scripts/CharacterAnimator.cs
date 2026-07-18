using System;
using UnityEngine;

public class CharacterAnimator : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Transform scaleNode;
    [SerializeField] private DirMode dirType = DirMode.Two;
    public enum DirMode { Two = 0, Four = 1, Eight = 2 }
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
        if (animator.GetInteger("Dir") != notFlipDir) animator.SetInteger("Dir", notFlipDir);
        scaleNode.localScale = new Vector3(oriScale.x * (isFlip ? -1 : 1), oriScale.y, oriScale.z);
    }

    public void TriggerAttack()
    {
        UpdateState(2);
        animator.SetTrigger("Attack");
    }

    // Raised by an AnimationEvent at the frame an attack connects. The attack logic lives on the
    // actor and listens here — the view only relays the timing.
    public event Action Hit;
    public void OnHit() => Hit?.Invoke();

    private (int curDir, bool isFlip) CalculateDir(int dir)
    {
        switch (dirType)
        {
            case DirMode.Two:
                return (1, dir != 1);
            case DirMode.Four:
                return (dir == 3 ? 1 : dir, dir == 3);
        }
        return default;
    }
}
