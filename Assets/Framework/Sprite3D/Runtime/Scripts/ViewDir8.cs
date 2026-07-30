using UnityEngine;

// The eight-direction sibling of ViewDir2, for a sprite lying FLAT ON THE GROUND that wants to read as a slab with
// thickness - a bridge deck, a jetty, a platform, a step.
//
// The quad is nailed to the world, so the camera orbiting already turns it on screen and the planks drawn on it
// keep pointing the way they should by themselves. The one thing geometry cannot do is show the slab's SIDE: a
// flat quad has no thickness. So the art bakes the side faces in, and this picks the drawing whose sides land on
// the edges the camera can actually see.
//
// EIGHT VIEWS, THREE SPRITES. From a cardinal angle the camera sees one face; from a diagonal it sees two. Mirror
// those and the eight views are three drawings:
//
//     camera N    south face        north
//     camera NE   south + west      northEast
//     camera E    west face         east
//     camera SE   north + west      northEast   flipY
//     camera S    north face        north       flipY
//     camera SW   north + east      northEast   flipX flipY
//     camera W    east face         east        flipX
//     camera NW   south + east      northEast   flipX
//
// MIRRORED, NOT ROTATED - and that is the whole reason there are three sprites instead of one. Turning the sprite
// a quarter would move the side face to the next edge, but it would carry the PLANKS round with it, and the planks
// run across the bridge in the world. Mirroring moves the side and leaves them alone. It is also why `east` has to
// be drawn rather than derived: same planks, different side.
[RequireComponent(typeof(SpriteRenderer))]
public class ViewDir8 : MonoBehaviour
{
    [Tooltip("Camera looking along this sprite's own +V axis: the near side face is its BOTTOM edge.")]
    [SerializeField] Sprite north;

    [Tooltip("Camera a quarter turn clockwise from that: the near side face is its LEFT edge.")]
    [SerializeField] Sprite east;

    [Tooltip("Camera halfway between the two: both faces, meeting at the BOTTOM-LEFT corner.")]
    [SerializeField] Sprite northEast;

    [Tooltip("Steps the whole table round by 45 degrees, for art drawn against a different axis than this one.")]
    [SerializeField, Range(0, 7)] int turn;

    SpriteRenderer sr;
    int lastViewDir = -1;
    Quaternion lastRot;

    void Awake() => sr = GetComponent<SpriteRenderer>();

    void OnEnable() => UpdateImage();

    // The slab's OWN rotation is the second thing that can change the answer - a deck laid diagonally across the
    // map shows a different side than one laid north-south, under the very same camera. Hence the rotation compare
    // next to the camera's; ViewDir2 needs only the camera because it has no such axis of its own.
    void LateUpdate()
    {
        if (!CameraViewDir.TransformChanged && transform.rotation == lastRot) return;
        UpdateImage();
    }

    void UpdateImage()
    {
        lastRot = transform.rotation;

        int viewDir = ViewDir();
        if (viewDir == lastViewDir) return;
        lastViewDir = viewDir;

        switch (viewDir)
        {
            case 0: Set(north,      false, false); break;
            case 1: Set(northEast,  false, false); break;
            case 2: Set(east,       false, false); break;
            case 3: Set(northEast,  false, true);  break;
            case 4: Set(north,      false, true);  break;
            case 5: Set(northEast,  true,  true);  break;
            case 6: Set(east,       true,  false); break;
            default: Set(northEast, true,  false); break;
        }
    }

    // A missing sprite keeps whatever is on the renderer rather than blanking it: half-filled art should look
    // wrong from some angles, not disappear from them.
    void Set(Sprite s, bool flipX, bool flipY)
    {
        if (s != null) sr.sprite = s;
        sr.flipX = flipX;
        sr.flipY = flipY;
    }

    // Which of the eight the camera sits in, measured against THIS sprite rather than against the world.
    //
    // Read off `transform.up` and not eulerAngles.y: a ground quad is rotated 90 degrees about X, which is exactly
    // the gimbal-locked pose where Unity is free to hand the yaw back as roll instead. The +V axis as a world
    // direction has no such ambiguity, and it is the axis the table above is written against anyway.
    int ViewDir()
    {
        Vector3 up = transform.up;
        float slabYaw = Mathf.Atan2(up.x, up.z) * Mathf.Rad2Deg;
        float camYaw = CameraViewDir.Transform != null ? CameraViewDir.Transform.eulerAngles.y : 0f;
        return (ViewAngleUtil.GetViewType8(camYaw - slabYaw) + turn) & 7;
    }
}
