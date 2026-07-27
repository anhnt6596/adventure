using System;
using UnityEngine;

public class UnitAnimator : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Transform scaleNode;
    [SerializeField] private DirMode dirType = DirMode.Two;
    [SerializeField] bool isMirror = true;
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

    public void UpdateDir(int dir8)
    {
        int notFlipDir;
        (notFlipDir, isFlip) = CalculateDir(dir8);
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
    private (int dir, bool isFlip) CalculateDir(int dir8)
    {
        switch (dirType)
        {
            case DirMode.Two:                       // one side profile: face right, mirror to face left
                return isMirror ? (1, dir8 >= 5) : (dir8 >= 5 ? (1, false) : (0, false));
            case DirMode.Four:                      // Up / Right / Down; Left = Right mirrored, diagonals fold to their side
                if (dir8 == 0) return (0, false);
                if (dir8 == 4) return (2, false);
                return isMirror ? (1, dir8 >= 5) : (dir8 >= 5 ? (3, false) : (1, false));
            case DirMode.Eight:                     // Up, UpRight, Right, DownRight, Down; left half mirrored
                // TODO: fix with isMirror later
                return dir8 <= 4 ? (dir8, false) : (8 - dir8, true);
        }
        return default;
    }
}
