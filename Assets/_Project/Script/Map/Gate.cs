using UnityEngine;

// A spawn point inside a map: where the player appears when arriving here. Position + facing.
public class Gate : MonoBehaviour
{
    public Vector3 SpawnPosition => transform.position;
    public Quaternion SpawnRotation => transform.rotation;

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.4f, 1f, 0.5f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.4f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1f);  // facing
    }
}
