using System;

[Flags]
public enum InputKind
{
    None   = 0,
    Move   = 1 << 0,
    Attack = 1 << 1,
    Camera = 1 << 2,

    Character = Move | Attack,
    All       = Move | Attack | Camera,
}
