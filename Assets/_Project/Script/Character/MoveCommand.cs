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

    // Always true: a direction held is always taken, there is no state that can refuse it. Movement is never
    // buffered anyway — it is read from what is HELD this frame, so remembering an old one would be steering
    // the character by a key nobody has a finger on.
    public bool Execute()
    {
        _input.AccumulateMove(_direction);
        return true;
    }
}
