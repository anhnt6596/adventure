using UnityEngine;

public class FollowWithCameraOffset : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float offsetDistance = 0.5f;
    [SerializeField] private bool faceCamera = false;

    private void LateUpdate()
    {
        Vector3 camPos = CameraViewDir.Transform.position;
        Vector3 targetPos = target.position;

        Vector3 dirFromCam = (targetPos - camPos).normalized;

        Vector3 finalPos = targetPos - dirFromCam * offsetDistance;

        transform.position = finalPos;

        if (faceCamera)
            transform.rotation = Quaternion.LookRotation(dirFromCam);
    }
}