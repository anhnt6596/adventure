using UnityEngine;

// Keeps the light camera's PROJECTION an exact twin of the main one, so the light RT is a screen-space image
// of the Light layer and the veil can sample it by screen-UV.
//
// Position and rotation are not its business: the light camera is a CHILD of the main one, so the hierarchy
// already gives it those for free, every frame, in the right order. Copying them here as well would be work
// that changes nothing — and it is worth saying so, because a script called LightCamera is the obvious place
// for someone to put that copy back.
//
// IT MIRRORS THE PROJECTION MATRIX, not a list of the fields that make one. Copying fieldOfView by hand looks
// like it mirrors the camera and does not: it misses aspect, the clip planes, lens shift, and — the one that
// actually bit — PHYSICAL CAMERA properties. A physical camera derives its projection from focal length and
// sensor size, so assigning fieldOfView to it is simply overwritten, and the light camera silently keeps the
// projection it had while the main camera moved on. One matrix cannot drift and cannot miss a field somebody
// adds later.
//
// Reassigned only WHEN IT DIFFERS. The projection does not change while the game runs, so this is idle in
// play — but the moment it does change is you dragging FoV in the inspector, which is edit mode, and that is
// exactly where a "set it once at Start" version would leave it wrong. A matrix compare covers both for
// nothing.
[ExecuteAlways]
public class LightCamera : MonoBehaviour
{
    public Camera mainCam;
    public Camera lightCam;

    Matrix4x4 _lastProj = Matrix4x4.zero;   // zero is not a projection, so the first tick always applies

    void LateUpdate()
    {
        if (!mainCam) mainCam = Camera.main;
        if (!lightCam) lightCam = GetComponent<Camera>();
        if (!mainCam || !lightCam) return;   // nothing to mirror yet - do not NRE every frame

        var proj = mainCam.projectionMatrix;
        if (proj == _lastProj) return;
        _lastProj = proj;

        // Kept in step for anything that READS these off the light camera. The matrix is what actually
        // decides the image; these only make the twin describe itself honestly.
        lightCam.orthographic = mainCam.orthographic;
        lightCam.fieldOfView = mainCam.fieldOfView;
        lightCam.orthographicSize = mainCam.orthographicSize;
        lightCam.nearClipPlane = mainCam.nearClipPlane;
        lightCam.farClipPlane = mainCam.farClipPlane;

        lightCam.projectionMatrix = proj;
    }
}
