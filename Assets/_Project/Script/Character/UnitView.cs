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
    }

    protected virtual void OnEnable() => character.Attacked += PlayAttack;
    protected virtual void OnDisable() => character.Attacked -= PlayAttack;

    // Push the aim BEFORE the trigger. The Animator consumes triggers in its own update, which runs after
    // Update — where the attack fires — and before LateUpdate, so leaving the direction to LateUpdate would
    // pick the attack state off the PREVIOUS frame's facing. An AI that turns to its target and swings in the
    // same Update (EnemyAI.TickAttack does exactly that) would swing the way it used to be turned.
    //
    // The swing also plays as fast as the unit swings, so the clip stretches with the busy window instead of
    // being cut off or leaving a dead tail — and the Hit AnimationEvent inside it lands at the same fraction
    // of the swing at every attack speed, no separate timing to keep in sync.
    void PlayAttack()
    {
        PushDir();
        characterAnimator.PlaybackSpeed = character.AttackRate;
        characterAnimator.TriggerAttack();
    }

    protected virtual void LateUpdate()
    {
        // Aim goes out EVERY frame, mid-swing included. The attack lock can run for seconds while the AI keeps
        // turning to track its target, and on a mirrored unit the facing IS the sprite flip — freezing it
        // leaves the whole swing aimed where the fight started. Safe to keep pushing: entering an attack state
        // needs the Attack trigger, so a direction change on its own can't restart the swing.
        PushDir();

        if (character.IsBusy) return;   // mid-swing: State stays on attack, don't let idle/move claim it back
        characterAnimator.PlaybackSpeed = 1f;   // swing over — idle/move play at their authored rate

        bool moving = character.Velocity.sqrMagnitude > 0.0001f;
        characterAnimator.UpdateState(moving ? 1 : 0);
    }

    // World facing minus the camera's own view sector = which way the unit reads on screen. Recomputed every
    // frame (not just while moving), so orbiting the camera re-aims a standing unit's sprite.
    void PushDir()
    {
        int screenDir = (character.Facing - CameraViewDir.CurrentViewDir8 + 8) % 8;
        characterAnimator.UpdateDir(screenDir);
    }
}
