using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraInput : MonoBehaviour
{
    [SerializeField] CameraRig rig;
    [SerializeField] float snapStep = 45f;

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

        foreach (var binding in _bindings)
            if (kb[binding.Key].wasPressedThisFrame)
                binding.Value.Execute(rig);
    }
}
