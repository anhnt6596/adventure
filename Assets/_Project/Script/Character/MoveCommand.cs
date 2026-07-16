using UnityEngine;

public class MoveCommand : ICharacterCommand
{
    readonly CharacterInput _input;
    readonly Vector2 _direction;

    public MoveCommand(CharacterInput input, Vector2 direction)
    {
        _input = input;
        _direction = direction;
    }

    public void Execute() => _input.AccumulateMove(_direction);
}
