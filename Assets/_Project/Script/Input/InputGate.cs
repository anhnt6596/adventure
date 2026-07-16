using System;
using System.Collections.Generic;

public class InputGate : IInputGate
{
    readonly List<Handle> _blocks = new List<Handle>();

    public bool Allows(InputKind kind)
    {
        for (int i = 0; i < _blocks.Count; i++)
            if ((_blocks[i].Kinds & kind) != 0) return false;
        return true;
    }

    public IDisposable Block(InputKind kinds, string reason = null)
    {
        var h = new Handle(this, kinds, reason);
        _blocks.Add(h);
        return h;
    }

    public string Describe()
    {
        if (_blocks.Count == 0) return "input: all allowed";
        var parts = new string[_blocks.Count];
        for (int i = 0; i < _blocks.Count; i++) parts[i] = $"{_blocks[i].Reason}->{_blocks[i].Kinds}";
        return "blocked by " + string.Join(", ", parts);
    }

    sealed class Handle : IDisposable
    {
        readonly InputGate _owner;
        public readonly InputKind Kinds;
        public readonly string Reason;
        bool _disposed;

        public Handle(InputGate owner, InputKind kinds, string reason)
        {
            _owner = owner; Kinds = kinds; Reason = reason ?? "unnamed";
        }

        public void Dispose()
        {
            if (_disposed) return;      // double-dispose must not release someone else's block
            _disposed = true;
            _owner._blocks.Remove(this);
        }
    }
}
