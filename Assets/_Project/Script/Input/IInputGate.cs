using System;

// Gates player input by kind. Blocks stack: two blockers + one release still blocks.
public interface IInputGate
{
    bool Allows(InputKind kind);

    /// <summary>Blocks the given kinds until the returned handle is disposed.</summary>
    IDisposable Block(InputKind kinds, string reason = null);

    /// <summary>Who is currently blocking what (debug).</summary>
    string Describe();
}
