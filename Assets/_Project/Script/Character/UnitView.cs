using UnityEngine;

// Drives a unit's sprite/animator off its control state (Velocity, IsBusy, Attacked). Typed to DynamicUnit,
// so the same view logic serves the player and any enemy — each unit kind gets a thin subclass (MCView,
// EnemyView) for its own view extras, while this holds the shared movement/attack animation.
public class UnitView : MonoBehaviour
{
    [SerializeField] protected DynamicUnit character;
    [SerializeField] protected UnitAnimator characterAnimator;

    protected virtual void Awake()
    {
        if (character == null) character = GetComponent<DynamicUnit>();
        if (characterAnimator == null) characterAnimator = GetComponentInChildren<UnitAnimator>();

        // The view owns the animation, so it is the only thing that can measure the swing. Pushed once —
        // the set does not change at runtime — so the combat lock never has to restate the clip's length.
        if (character != null && characterAnimator != null)
            character.SetSwingClipLength(characterAnimator.LengthOf(AnimAction.Attack));
    }

    protected virtual void OnEnable() => character.Attacked += PlayAttack;
    protected virtual void OnDisable() => character.Attacked -= PlayAttack;

    // Aim first, then start the swing, so the very first frame drawn is already the right side of the unit —
    // an AI that turns to its target and attacks in the same Update (EnemyAI.TickAttack does exactly that)
    // would otherwise show one frame of the old facing.
    //
    // The swing plays as fast as the unit swings, so the clip stretches with the busy window instead of being
    // cut off or leaving a dead tail — and the hit frame inside it lands at the same fraction of the swing at
    // every attack speed, no separate timing to keep in sync.
    void PlayAttack()
    {
        PushDir();
        characterAnimator.PlaybackSpeed = character.AttackRate;
        characterAnimator.Play(AnimAction.Attack);
    }

    protected virtual void LateUpdate()
    {
        // Aim goes out EVERY frame, mid-swing included. The attack lock can run for seconds while the AI keeps
        // turning to track its target; the animator re-reads the direction without disturbing the playhead, so
        // this only ever changes which side of the swing is drawn, never how far along it is.
        PushDir();

        if (character.IsBusy) return;   // mid-swing: leave the action alone, don't let idle/move claim it back
        characterAnimator.PlaybackSpeed = 1f;   // swing over — idle/move play at their authored rate

        bool moving = character.Velocity.sqrMagnitude > 0.0001f;
        characterAnimator.Play(moving ? AnimAction.Move : AnimAction.Idle);
    }

    // Which way the unit reads on screen, as an angle. The UNSNAPPED aim brought into the camera's own frame
    // — where +x is screen-right and +z is screen-up — which is exactly how MCInput turns key presses into
    // world movement, run backwards. Handing over the raw angle rather than a sector lets each anim set
    // quantise it however its own pose count needs. Recomputed every frame, not just while moving, so
    // orbiting the camera re-aims a standing unit's sprite.
    void PushDir()
    {
        float camYaw = CameraViewDir.Transform != null ? CameraViewDir.Transform.eulerAngles.y : 0f;
        Vector3 screen = Quaternion.Euler(0f, -camYaw, 0f) * character.AimRaw;
        characterAnimator.SetDir(Mathf.Atan2(screen.x, screen.z) * Mathf.Rad2Deg);
    }
}
