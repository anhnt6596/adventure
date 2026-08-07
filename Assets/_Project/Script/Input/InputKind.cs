using System;

[Flags]
public enum InputKind
{
    None   = 0,
    Move   = 1 << 0,
    Attack = 1 << 1,
    Camera = 1 << 2,

    // Its own flag rather than riding on Attack: a cutscene or a shop may well want the character able to
    // swing but not to leave the spot, and a dash is movement wearing a button.
    Skill  = 1 << 3,

    Character = Move | Attack | Skill,
    All       = Move | Attack | Skill | Camera,
}
