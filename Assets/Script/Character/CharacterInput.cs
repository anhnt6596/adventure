using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CharacterInput : MonoBehaviour
{
    [SerializeField] Character character;
    [SerializeField] Transform cameraTransform;

    readonly Dictionary<Key, ICharacterCommand> _held = new Dictionary<Key, ICharacterCommand>();
    readonly Dictionary<Key, ICharacterCommand> _pressed = new Dictionary<Key, ICharacterCommand>();
    Vector2 _localMove;

    void Awake()
    {
        if (character == null) character = GetComponent<Character>();
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;

        var up = new MoveCommand(this, Vector2.up);
        var down = new MoveCommand(this, Vector2.down);
        var left = new MoveCommand(this, Vector2.left);
        var right = new MoveCommand(this, Vector2.right);

        _held[Key.W] = up;
        _held[Key.S] = down;
        _held[Key.A] = left;
        _held[Key.D] = right;
        _held[Key.UpArrow] = up;
        _held[Key.DownArrow] = down;
        _held[Key.LeftArrow] = left;
        _held[Key.RightArrow] = right;

        _pressed[Key.Space] = new AttackCommand(character);
    }

    public void AccumulateMove(Vector2 direction) => _localMove += direction;

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        _localMove = Vector2.zero;
        foreach (var b in _held)
            if (kb[b.Key].isPressed) b.Value.Execute();

        if (_localMove != Vector2.zero && cameraTransform != null)
        {
            float camYaw = cameraTransform.eulerAngles.y;
            var world = Quaternion.Euler(0f, camYaw, 0f) * new Vector3(_localMove.x, 0f, _localMove.y);
            character.Move(new Vector2(world.x, world.z));
        }

        foreach (var b in _pressed)
            if (kb[b.Key].wasPressedThisFrame) b.Value.Execute();
    }
}
