using System;
using UnityEngine;

public class UnitAnimator : MonoBehaviour
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

    // `dir` is a screen-relative 8-sector index, clockwise from Up:
    //   0 Up, 1 UpRight, 2 Right, 3 DownRight, 4 Down, 5 DownLeft, 6 Left, 7 UpLeft.
    // Left-facing sectors (5..7) reuse the right-facing frames, mirrored via the scaleNode flip. Each mode
    // folds the eight sectors down to the frames its sheet actually has; the returned int is the "Dir" param.
    private (int curDir, bool isFlip) CalculateDir(int dir)
    {
        switch (dirType)
        {
            case DirMode.Two:                       // one side profile: face right, mirror to face left
                return (1, dir >= 5);
            case DirMode.Four:                      // Up / Right / Down; Left = Right mirrored, diagonals fold to their side
                if (dir == 0) return (0, false);
                if (dir == 4) return (2, false);
                return (1, dir >= 5);
            case DirMode.Eight:                     // Up, UpRight, Right, DownRight, Down; left half mirrored
                return dir <= 4 ? (dir, false) : (8 - dir, true);
        }
        return default;
    }
}
