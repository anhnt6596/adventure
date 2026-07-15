using UnityEngine;

public class CameraViewDir : MonoBehaviour
{
    public static int CurrentViewDir2 { get; private set; } = 0;
    public static int CurrentViewDir8 { get; private set; } = 0;

    public static bool TransformChanged { get; private set; } = false;
    public static Vector3 CamForward { get; private set; }
    public static Transform Transform { get; private set; }

    private void OnEnable()
    {
        Transform = transform;
        UpdateCamInfo();
    }

    private void Update()
    {
        var curForward = transform.forward;

        if (Vector3.Distance(curForward, CamForward) > 0.0001f)
        {
            TransformChanged = true;
            UpdateCamInfo();
        }
        else
        {
            TransformChanged = false;
        }
    }

    private void UpdateCamInfo()
    {
        // update forward vector
        CamForward = transform.forward;

        // update direction
        float angleY = transform.eulerAngles.y;
        CurrentViewDir8 = ViewAngleUtil.GetViewType8(angleY);
        CurrentViewDir2 = CurrentViewDir8 % 2;
    }
}