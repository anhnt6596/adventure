using UnityEngine;

// A place inside a map where the player can be put down: position + facing. Arriving from anywhere — a
// Portal from another map, or stepping back out of a gate — lands on one of these.
//
// It is a MARKER, not a doorway. What sends you here is a Portal or a Gate; this end only says where you
// stand and which way you look. (It used to be called Gate, which read as a doorway and collided with the
// thing you actually walk into — see Gate.)
public class SpawnPoint : MonoBehaviour
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
