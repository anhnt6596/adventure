using UnityEngine;

// Drives a unit's sprite/animator off its control state (Velocity, IsBusy). Typed to DynamicUnit, so the same
// view logic serves the player and any enemy — each unit kind gets a thin subclass (MCView, EnemyView) for its
// own view extras, while this holds the shared movement animation.
//
// IT DOES NOT PLAY ATTACKS. A blow owns its own clip (AttackAbility), because which clip an attack plays is a
// property of that attack — a five-hit string has five — and a view that picked one could only ever be right
// for a character with exactly one. All this does is stay out of the way while the unit is committed.
public class UnitView : MonoBehaviour
{
    [SerializeField] protected DynamicUnit character;
    [SerializeField] protected UnitAnimator characterAnimator;

    protected virtual void Awake()
    {
        if (character == null) character = GetComponent<DynamicUnit>();
        if (characterAnimator == null) characterAnimator = GetComponentInChildren<UnitAnimator>();
    }

    protected virtual void LateUpdate()
    {
        // Aim goes out EVERY frame, mid-swing included. The attack lock can run for seconds while the AI keeps
        // turning to track its target; the animator re-reads the direction without disturbing the playhead, so
        // this only ever changes which side of the swing is drawn, never how far along it is.
        PushDir();

        // HANDS OFF WHILE THE UNIT IS COMMITTED. Whatever is running — a blow, a dash — owns the animator for
        // that window and drives it itself, because those do not share one shape: some play nothing, some run
        // several clips in order, some pick by what is going on. Anything this method played would be fighting
        // whichever of those is happening.
        //
        // Aim above still goes out, so the sprite keeps turning; only the ACTION is left alone.
        //
        // It is also what makes cancelling a swing real rather than cosmetic: a blow lands its damage on the
        // animator's Hit frame, so when a dash replaces the clip mid-swing the hit never arrives.
        if (character.IsBusy) return;
        characterAnimator.PlaybackSpeed = 1f;   // swing over — idle/move play at their authored rate

        bool moving = character.Velocity.sqrMagnitude > 0.0001f;
        characterAnimator.Play(moving ? AnimAction.Move : AnimAction.Idle);
    }

    // Which way the unit reads on screen, as an angle. The unit's aim brought into the camera's own frame
    // — where +x is screen-right and +z is screen-up — which is exactly how MCInput turns key presses into
    // world movement, run backwards. Handing over the raw angle rather than a sector lets each anim set
    // quantise it however its own pose count needs. Recomputed every frame, not just while moving, so
    // orbiting the camera re-aims a standing unit's sprite.
    void PushDir()
    {
        float camYaw = CameraViewDir.Transform != null ? CameraViewDir.Transform.eulerAngles.y : 0f;
        Vector3 screen = Quaternion.Euler(0f, -camYaw, 0f) * character.FacingDir;
        characterAnimator.SetDir(Mathf.Atan2(screen.x, screen.z) * Mathf.Rad2Deg);
    }
}
