using UnityEngine;

public class CharacterView : MonoBehaviour
{
    [SerializeField] Character character;
    [SerializeField] CharacterAnimator characterAnimator;
    [SerializeField] Transform cameraTransform;

    void Awake()
    {
        if (character == null) character = GetComponent<Character>();
        if (characterAnimator == null) characterAnimator = GetComponentInChildren<CharacterAnimator>();
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
    }

    void OnEnable() => character.Attacked += PlayAttack;
    void OnDisable() => character.Attacked -= PlayAttack;

    void PlayAttack() => characterAnimator.TriggerAttack();

    void LateUpdate()
    {
        if (character.IsBusy) return;

        var v = character.Velocity;
        bool moving = v.sqrMagnitude > 0.0001f;

        characterAnimator.UpdateState(moving ? 1 : 0);

        if (!moving || cameraTransform == null) return;

        var dir = MovingUtils.GetDirection2Index(v, cameraTransform);
        if (dir != Dir2.Unknown) characterAnimator.UpdateDir((int)dir);
    }
}
