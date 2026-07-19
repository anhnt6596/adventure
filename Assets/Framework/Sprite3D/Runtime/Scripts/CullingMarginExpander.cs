using UnityEngine;
using UnityEngine.Rendering;

// Widens the camera's CULLING frustum (not its render frustum — the image is unchanged) so tall billboards
// and their ground shadows aren't culled when their pivot drifts just off-screen while the visible part is
// still in frame. Ground-shadow quads sit at sprite height (raised so the base rests on the ground), so a
// far tree near the top of the view can have its quad pivot above the frame and get culled — dropping the
// shadow even though it falls into view. This keeps those quads rendering.
//
// Put it on the main camera. Trade-off: a margin of just-off-screen objects is drawn (then clipped), so
// dial `margin` down if it costs too much; up if shadows still pop out at the edges.
[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public class CullingMarginExpander : MonoBehaviour
{
    [SerializeField, Range(0f, 3f)] float margin = 0.6f;   // fraction to widen the culling frustum by

    Camera _cam;

    void OnEnable()
    {
        _cam = GetComponent<Camera>();
        RenderPipelineManager.beginCameraRendering += OnBeginCamera;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        if (_cam != null) _cam.ResetCullingMatrix();
    }

    void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
    {
        if (cam != _cam) return;
        float k = 1f + margin;
        Matrix4x4 p = _cam.projectionMatrix;
        p.m00 /= k;   // widen horizontally
        p.m11 /= k;   // widen vertically
        _cam.cullingMatrix = p * _cam.worldToCameraMatrix;
    }
}
