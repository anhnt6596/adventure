using UnityEngine;

public interface ICameraMode
{
    void Enter(CameraRig rig) { }
    void Exit(CameraRig rig) { }
    (Vector3 position, Quaternion rotation) Solve(CameraRig rig, float dt);
}
