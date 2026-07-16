using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

public class CameraInput : MonoBehaviour
{
    [SerializeField] CameraRig rig;
    [SerializeField] float snapStep = 45f;

    IInputGate _gate;

    [Inject]
    public void Construct(IInputGate gate) => _gate = gate;

    void Start()
    {
        if (_gate == null)
            Debug.LogError($"[{nameof(CameraInput)}] IInputGate not injected — add this GameObject to GameScope's Auto Inject Game Objects; camera input gating is disabled.", this);
    }

    readonly Dictionary<Key, ICameraCommand> _bindings = new Dictionary<Key, ICameraCommand>();

    void Awake()
    {
        _bindings[Key.Q] = new RotateYawCommand(-snapStep);
        _bindings[Key.E] = new RotateYawCommand(snapStep);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || rig == null) return;
        if (_gate != null && !_gate.Allows(InputKind.Camera)) return;

        foreach (var binding in _bindings)
            if (kb[binding.Key].wasPressedThisFrame)
                binding.Value.Execute(rig);
    }
}
