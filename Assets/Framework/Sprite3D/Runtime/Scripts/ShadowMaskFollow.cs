using UnityEngine;

[ExecuteAlways]
public class MaskFollowCamera : MonoBehaviour
{
    public Camera targetCamera;
    public float distance = 0.5f;

    private void LateUpdate()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera == null) return;

        transform.position = targetCamera.transform.position + targetCamera.transform.forward * distance;
        transform.rotation = targetCamera.transform.rotation;

        if (targetCamera.orthographic)
        {
            // --- ORTHOGRAPHIC ---
            float height = targetCamera.orthographicSize * 2f;
            float width = height * targetCamera.aspect;
            transform.localScale = new Vector3(width, height, 1f);
        }
        else
        {
            // --- PERSPECTIVE ---
            float height = Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * distance * 2f;
            float width = height * targetCamera.aspect;
            transform.localScale = new Vector3(width, height, 1f);
        }
    }
}