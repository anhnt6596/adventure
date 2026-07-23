using VContainer;
using VContainer.Unity;

// Policy glue: keep the camera aimed at whatever body PlayerSystem currently owns. CameraRig stays a generic
// rig (target set from outside) and PlayerSystem stays camera-agnostic (it just fires Spawned) — this binds
// the two, and re-aims automatically on respawn / character-switch.
public class CameraFollowsPlayer : IStartable
{
    readonly IPlayer _player;
    readonly CameraRig _camera;

    [Inject]
    public CameraFollowsPlayer(IPlayer player, CameraRig camera)
    {
        _player = player;
        _camera = camera;
    }

    public void Start()
    {
        _player.Spawned += Aim;
        if (_player.Current != null) Aim(_player.Current);   // player may already be spawned before this runs
    }

    void Aim(Character c) => _camera.Target = c.transform;
}
