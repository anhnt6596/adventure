using UnityEngine;

public class MoveCommand : ICharacterCommand
{
    readonly MCInput _input;
    readonly Vector2 _direction;

    public MoveCommand(MCInput input, Vector2 direction)
    {
        _input = input;
        _direction = direction;
    }

    public void Execute() => _input.AccumulateMove(_direction);
}
