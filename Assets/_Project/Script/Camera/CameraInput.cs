using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

public class CameraInput : MonoBehaviour
{
    [SerializeField] CameraRig rig;
    [SerializeField] float snapStep = 45f;

    [Tooltip("World units the camera moves per notch of the wheel. How far it may go in or out at all belongs " +
             "to the rig (its min/max distance), not here.")]
    [SerializeField] float zoomStep = 1f;

    // A wheel notch does not arrive as one number. Some backends hand over the raw platform tick — 120 on
    // Windows — and others have already normalised it to 1. Picking either and dividing by it is wrong half
    // the time, and wrong QUIETLY: assume 120 where 1 is sent and the wheel moves the camera by a hundred and
    // twentieth of a step, which reads as "zoom does nothing" rather than as a bug.
    //
    // So measure it instead. Anything of that magnitude is raw ticks; anything small is already a count of
    // notches. A trackpad sends fractions under either convention, and both branches leave those alone — a
    // two-finger drag keeps zooming smoothly instead of falling into a dead zone below one notch.
    const float RawTick = 120f;

    static float Notches(float scroll) => Mathf.Abs(scroll) >= 10f ? scroll / RawTick : scroll;

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
        if (rig == null) return;
        if (_gate != null && !_gate.Allows(InputKind.Camera)) return;

        var kb = Keyboard.current;
        if (kb != null)
        {
            foreach (var binding in _bindings)
                if (kb[binding.Key].wasPressedThisFrame)
                    binding.Value.Execute(rig);
        }

        // NOT AN ICameraCommand, and that is the point of the distinction: a command is one press asking for
        // one thing, while a wheel is an axis — it arrives with an amount, and a command object built to carry
        // one would have to be made afresh every frame the wheel turned to say how much.
        //
        // Wheel forward pulls the camera IN, which is the direction every other game agrees on.
        var mouse = Mouse.current;
        if (mouse == null) return;

        float scroll = mouse.scroll.ReadValue().y;
        if (scroll != 0f) rig.Zoom(-Notches(scroll) * zoomStep);
    }
}
