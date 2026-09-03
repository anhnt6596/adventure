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

    // NO ZOOM. The wheel used to pull the camera in and out, and it was taken away rather than tuned: an
    // arena spawns its monsters on a ring authored just outside the view (ArenaConfig.spawnRing), and a view
    // the player can resize turns that one number into a promise the game cannot keep — zoom out and the ring
    // is on screen, zoom in and arrivals come from absurdly far away. A fixed shot is what makes "just out of
    // sight" a thing anybody can author.
    void Update()
    {
        if (rig == null) return;
        if (_gate != null && !_gate.Allows(InputKind.Camera)) return;

        var kb = Keyboard.current;
        if (kb != null)
        {
            foreach (var binding in _bindings)
                if (kb[binding.Key].wasPressedThisFrame)
                    binding.Value.Execute(rig);
        }

    }
}
