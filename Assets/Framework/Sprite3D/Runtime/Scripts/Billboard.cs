using UnityEngine;

// Registers into BillboardManager on enable; the manager rotates all billboards
// from a single LateUpdate. No per-object LateUpdate here.
public class Billboard : MonoBehaviour
{
    private void OnEnable()
    {
        var f = CameraViewDir.CamForward;
        if (f.sqrMagnitude > 1e-6f) transform.forward = f;   // snap to face camera on spawn
        BillboardManager.Register(transform);
    }

    private void OnDisable()
    {
        BillboardManager.Unregister(transform);
    }
}
