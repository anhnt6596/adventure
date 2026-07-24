using UnityEngine;

// Drives a unit's sprite/animator off its control state (Velocity, IsBusy, Attacked). Typed to UnitController,
// so the same view logic serves the player and any enemy — each unit kind gets a thin subclass (MCView,
// EnemyView) for its own view extras, while this holds the shared movement/attack animation.
public class UnitView : MonoBehaviour
{
    [SerializeField] protected UnitController character;
    [SerializeField] protected UnitAnimator characterAnimator;

    protected virtual void Awake()
    {
        if (character == null) character = GetComponent<UnitController>();
        if (characterAnimator == null) characterAnimator = GetComponentInChildren<UnitAnimator>();
    }

    protected virtual void OnEnable() => character.Attacked += PlayAttack;
    protected virtual void OnDisable() => character.Attacked -= PlayAttack;

    void PlayAttack() => characterAnimator.TriggerAttack();

    protected virtual void LateUpdate()
    {
        if (character.IsBusy) return;

        bool moving = character.Velocity.sqrMagnitude > 0.0001f;
        characterAnimator.UpdateState(moving ? 1 : 0);

        // World facing minus the camera's own view sector = which way the unit reads on screen. Recomputed
        // every frame (not just while moving), so orbiting the camera re-aims a standing unit's sprite.
        int screenDir = (character.Facing - CameraViewDir.CurrentViewDir8 + 8) % 8;
        characterAnimator.UpdateDir(screenDir);
    }
}
