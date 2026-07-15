using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class LightCamera : MonoBehaviour
{
    public Camera mainCam;
    public Camera lightCam;
    private void LateUpdate()
    {
        if (!mainCam) mainCam = Camera.main;
        if (!lightCam) lightCam = GetComponent<Camera>();

        transform.position = mainCam.transform.position;
        transform.rotation = mainCam.transform.rotation;

        // Mirror main cam exactly -> light RT is a screen-space image of the Light layer,
        // so the veil can sample it by screen-UV (alignment no longer depends on quad fit).
        lightCam.orthographic = mainCam.orthographic;
        lightCam.fieldOfView = mainCam.fieldOfView;
        lightCam.orthographicSize = mainCam.orthographicSize;
    }
}
