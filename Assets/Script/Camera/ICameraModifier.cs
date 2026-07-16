using UnityEngine;

public interface ICameraModifier
{
    (Vector3 position, Quaternion rotation) Apply(CameraRig rig, Vector3 position, Quaternion rotation);
}
