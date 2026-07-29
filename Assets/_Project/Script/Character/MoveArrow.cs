using UnityEngine;
using VContainer;

// A ground arrow that points where the MC is heading (its FacingDir) and rides along under it. It reads the
// CURRENT player each frame, so it follows a respawn / character-switch for free. Put this on the ArrowDir
// prefab, drop the prefab into the GameScene, and add it to GameScope's Auto Inject Game Objects (so it gets
// IPlayer). Author the art lying flat, pointing local +Z; use yawOffset if it points another way.
public class MoveArrow : MonoBehaviour
{
    [SerializeField] float forwardDistance = 0f;   // sit this far ahead of the MC along its facing (0 = at its feet)
    [SerializeField] float heightOffset = 0.02f;   // lift off the ground so it doesn't z-fight the floor
    [SerializeField] float yawOffset = 0f;         // correct if the art's point isn't local +Z

    IPlayer _player;
    Renderer[] _renderers;

    [Inject]
    public void Construct(IPlayer player) => _player = player;

    void Awake() => _renderers = GetComponentsInChildren<Renderer>(true);

    void Start()
    {
        if (_player == null)
            Debug.LogError($"[{nameof(MoveArrow)}] IPlayer not injected — add this GameObject to GameScope's Auto Inject Game Objects.", this);
    }

    // LateUpdate: the MC moves + refreshes FacingDir in Update, so read it after — the arrow never lags a frame.
    void LateUpdate()
    {
        var mc = _player != null ? _player.Current : null;
        SetVisible(mc != null);
        if (mc == null) return;

        Vector3 dir = mc.FacingDir; dir.y = 0f;
        if (dir.sqrMagnitude < 1e-6f) dir = Vector3.right;   // unreachable in practice (FacingDir is never zero); east to match the rest
        dir.Normalize();

        transform.SetPositionAndRotation(
            mc.transform.position + dir * forwardDistance + Vector3.up * heightOffset,
            Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(0f, yawOffset, 0f));
    }

    void SetVisible(bool visible)
    {
        if (_renderers == null) return;
        for (int i = 0; i < _renderers.Length; i++)
            if (_renderers[i] != null && _renderers[i].enabled != visible) _renderers[i].enabled = visible;
    }
}
