using UnityEngine;

public class FollowMode : ICameraMode
{
    Vector3 _pivot;
    bool _init;

    public void Enter(CameraRig rig)
    {
        _pivot = rig.Pivot;
        _init = true;
    }

    public (Vector3, Quaternion) Solve(CameraRig rig, float dt)
    {
        if (!_init) { _pivot = rig.Pivot; _init = true; }
        _pivot = Vector3.Lerp(_pivot, rig.Pivot, rig.Smooth);

        var rot = Quaternion.Euler(rig.Pitch, rig.Yaw, 0f);
        var pos = _pivot + rot * new Vector3(0f, 0f, -rig.Distance);
        return (pos, rot);
    }
}
