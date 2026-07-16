using UnityEngine;

// Registers into BillboardManager on enable; the manager rotates all billboards
// from a single LateUpdate (and orients newly registered ones even if the camera is still).
public class Billboard : MonoBehaviour
{
    private void OnEnable() => BillboardManager.Register(transform);
    private void OnDisable() => BillboardManager.Unregister(transform);
}
