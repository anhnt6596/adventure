using UnityEngine;

// Drives a unit's sprite/animator off its control state (Velocity, IsBusy, Attacked). Typed to UnitController,
// so the same view logic serves the player and any enemy — each unit kind gets a thin subclass (MCView,
// EnemyView) for its own view extras, while this holds the shared movement/attack animation.
public class UnitView : MonoBehaviour
{
    [SerializeField] protected UnitController character;
    [SerializeField] protected CharacterAnimator characterAnimator;
    [SerializeField] Dir2 startFacing = Dir2.Right;   // direction (and flip) before the first move

    protected virtual void Awake()
    {
        if (character == null) character = GetComponent<UnitController>();
        if (characterAnimator == null) characterAnimator = GetComponentInChildren<CharacterAnimator>();
    }

    // The dir/flip is only updated while moving, so give it a sensible facing up front — otherwise the
    // sprite (and its shadow) sit at the animator's default until the first step.
    protected virtual void Start() => characterAnimator.UpdateDir((int)startFacing);

    protected virtual void OnEnable() => character.Attacked += PlayAttack;
    protected virtual void OnDisable() => character.Attacked -= PlayAttack;

    void PlayAttack() => characterAnimator.TriggerAttack();

    protected virtual void LateUpdate()
    {
        if (character.IsBusy) return;

        var v = character.Velocity;
        bool moving = v.sqrMagnitude > 0.0001f;

        characterAnimator.UpdateState(moving ? 1 : 0);

        var cam = CameraViewDir.Transform;
        if (!moving || cam == null) return;

        var dir = MovingUtils.GetDirection2Index(v, cam);
        if (dir != Dir2.Unknown) characterAnimator.UpdateDir((int)dir);
    }
}
