public class RotateYawCommand : ICameraCommand
{
    readonly float _step;

    public RotateYawCommand(float step) => _step = step;

    public void Execute(CameraRig rig) => rig.RotateYaw(_step);
}
