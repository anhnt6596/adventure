using System.Collections.Generic;
using Core;
using UnityEngine;

// One LateUpdate for every FadeWhenBlocking in the world instead of N of them — same shape as BillboardManager:
// static, built on first Register, self-cleaning, no scene setup. It does the camera maths ONCE and hands the
// result to each blocker, which is most of the point: the projection of the player and the direction of the
// view ray are the same for all of them, and doing that per-tree would be the actual cost of this feature.
//
// Only blockers in the hash cells AROUND THE PLAYER are considered. Anything that can hide the player has to be
// between them and the camera, so the search radius is simply how far the camera is sitting back, and a map's
// worth of distant trees never gets touched. Blockers are static, so the hash is rebuilt on registration
// changes rather than per frame — a blocker that MOVES would need Rebuild called again (nothing does yet).
//
// Runs in LateUpdate so it reads positions after the player has moved and the camera rig has solved — a frame
// of lag here would show up as the tree clearing out a beat after you walked behind it.
public class BlockerFadeManager : MonoBehaviour
{
    static BlockerFadeManager _instance;

    // What has to stay visible. Set by CameraFollowsPlayer: the thing worth revealing is the thing the camera
    // is following, and that policy already lives there — this system never learns what a "player" is.
    public static Transform Subject { get; set; }

    const float CellSize = 8f;        // same order as the combat hash: a handful of cells covers the camera arm
    const float QueryMargin = 8f;     // slack for a blocker whose ART reaches into the ray from a cell further out

    readonly SpatialHash<FadeWhenBlocking> _hash = new(b => b.transform.position, CellSize);
    List<FadeWhenBlocking> _near = new();
    List<FadeWhenBlocking> _wasNear = new();
    bool _dirty;

    Camera _cam;
    Transform _boundSubject;             // who _subjectArt was resolved for
    SpriteRenderer[] _subjectArt;

    static BlockerFadeManager Instance
    {
        get
        {
            if (_instance != null) return _instance;
            var go = new GameObject("[BlockerFadeManager]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<BlockerFadeManager>();
            return _instance;
        }
    }

    public static void Register(FadeWhenBlocking b)
    {
        var m = Instance;
        m._hash.Add(b);
        m._dirty = true;
    }

    public static void Unregister(FadeWhenBlocking b)
    {
        if (_instance == null) return;
        _instance._hash.Remove(b);
        _instance._dirty = true;
    }

    void LateUpdate()
    {
        if (_hash.Items.Count == 0) return;
        if (_dirty) { _hash.Rebuild(); _dirty = false; }

        var cam = ResolveCamera();
        var subject = Subject;
        if (cam == null || subject == null) { Relax(); return; }

        // The subject is tested as a point HORIZONTALLY and as its full height VERTICALLY — see
        // FadeWhenBlocking.IsBlocking for why the two axes are asked different questions.
        if (!SubjectRect(cam, subject, out Rect subjectRect, out Vector3 subjectPos)) { Relax(); return; }

        Vector3 camPos = cam.transform.position;
        Vector3 toSubject = subjectPos - camPos;
        float subjectDist = toSubject.magnitude;
        if (subjectDist < 1e-4f) { Relax(); return; }
        Vector3 rayDir = toSubject / subjectDist;

        // Everything that could possibly be in the way lies between the camera and the player, so the camera's
        // own distance IS the search radius. No map size enters into it.
        _hash.Query(subjectPos, subjectDist + QueryMargin, _near);

        float dt = Time.deltaTime;
        for (int i = 0; i < _near.Count; i++)
        {
            var b = _near[i];
            if (b != null) b.Tick(cam, camPos, rayDir, subjectDist, subjectRect, dt);
        }

        // Anything that left the search area mid-fade still owes us the walk back to solid — carry it until it
        // gets there, or a tree you ran away from stays ghostly for the rest of the session.
        for (int i = 0; i < _wasNear.Count; i++)
        {
            var b = _wasNear[i];
            if (b == null || _near.Contains(b)) continue;
            b.Clear(dt);
            if (!b.Settled) _near.Add(b);
        }

        (_near, _wasNear) = (_wasNear, _near);
    }

    bool SubjectRect(Camera cam, Transform subject, out Rect rect, out Vector3 centre)
    {
        if (subject != _boundSubject)
        {
            _boundSubject = subject;
            _subjectArt = ArtRenderersOf(subject.gameObject);
        }

        if (ScreenRectOf(cam, _subjectArt, out rect))
        {
            centre = WorldBoundsOf(_subjectArt).center;
            return true;
        }

        // Nothing to measure (the spawn frame, or a bodiless subject) — a zero-height rect at chest level, so
        // the system degrades to the old point test instead of blinking off.
        centre = subject.position + Vector3.up * 0.5f;
        Vector3 s = cam.WorldToScreenPoint(centre);
        if (s.z <= 0f) return false;
        rect = new Rect(s.x, s.y, 0f, 0f);
        return true;
    }

    // No camera or no subject is a transient state (scene load, player respawn), not an error — let everything
    // return to solid rather than freezing whatever alpha it happened to hold.
    void Relax()
    {
        float dt = Time.deltaTime;
        for (int i = 0; i < _wasNear.Count; i++)
        {
            var b = _wasNear[i];
            if (b != null) b.Clear(dt);
        }
    }

    Camera ResolveCamera()
    {
        if (_cam != null) return _cam;
        var t = CameraViewDir.Transform;   // the same camera the billboards face
        _cam = t != null ? t.GetComponent<Camera>() : Camera.main;
        return _cam;
    }

    // The billboarded art, and only that. SpriteShadow builds its shadow renderer at runtime and hangs it off
    // the ROOT, deliberately outside the billboard, so "under the Billboard" excludes it without anyone having
    // to care who woke up first.
    internal static SpriteRenderer[] ArtRenderersOf(GameObject go)
    {
        var billboard = go.GetComponentInChildren<Billboard>(true);
        return billboard != null
            ? billboard.GetComponentsInChildren<SpriteRenderer>(true)
            : go.GetComponentsInChildren<SpriteRenderer>(true);
    }

    internal static Bounds WorldBoundsOf(SpriteRenderer[] renderers)
    {
        var bounds = new Bounds();
        bool any = false;
        if (renderers == null) return bounds;

        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null || !r.enabled) continue;
            if (!any) { bounds = r.bounds; any = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return any ? bounds : new Bounds();
    }

    // Half-diagonal of the sprite quad, in world units. NOT bounds.extents.magnitude: that is the world AABB of
    // a quad tilted to face the camera, so it swells and shrinks as the camera ORBITS — thin when the view runs
    // down an axis, fat on the diagonal. Any threshold built on it silently changes strength with yaw, which
    // reads as the effect working better from some angles than others. The quad's own diagonal cannot rotate.
    internal static float WorldRadiusOf(SpriteRenderer[] renderers)
    {
        float best = 0f;
        if (renderers == null) return 0f;

        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null || !r.enabled || r.sprite == null) continue;

            Vector3 e = r.sprite.bounds.extents;
            Vector3 s = r.transform.lossyScale;
            float x = e.x * s.x, y = e.y * s.y;
            float radius = Mathf.Sqrt(x * x + y * y);
            if (radius > best) best = radius;
        }
        return best;
    }

    // The sprites' real screen footprint: the FOUR corners of each billboard quad, projected.
    //
    // Not renderer.bounds. That is a world-space AABB, and a billboard tilted back to meet a camera looking
    // DOWN at any angle has an AABB much TALLER than the sprite in it — the box has to contain the quad's
    // far-top and near-bottom corners, neither of which is on the sprite. Width survives (the quad's horizontal
    // axis stays horizontal) but height gets inflated, so the top of the rect claims a band of sky above the
    // canopy where nothing is drawn. The worse the tilt the worse it gets, and it is worst exactly where a tree
    // needs to be right: its crown.
    //
    // Everything here comes off the live camera — the projection matrix, and the billboard's own axes, which
    // BillboardManager already aimed at it. No pitch is assumed or read anywhere, so changing the rig's angle
    // (or moving it at runtime) needs no matching change here.
    internal static bool ScreenRectOf(Camera cam, SpriteRenderer[] renderers, out Rect rect)
    {
        rect = default;
        if (renderers == null) return false;

        float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
        bool any = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null || !r.enabled || r.sprite == null) continue;

            var t = r.transform;
            Bounds local = r.sprite.bounds;                       // sprite units, centred on its pivot
            Vector3 centre = t.TransformPoint(local.center);
            Vector3 halfX = t.right * (local.extents.x * t.lossyScale.x);
            Vector3 halfY = t.up * (local.extents.y * t.lossyScale.y);

            for (int k = 0; k < 4; k++)
            {
                Vector3 corner = centre
                                 + ((k & 1) == 0 ? -halfX : halfX)
                                 + ((k & 2) == 0 ? -halfY : halfY);

                Vector3 s = cam.WorldToScreenPoint(corner);
                if (s.z <= 0f) return false;   // straddling the camera plane — the rect would be garbage
                if (s.x < xMin) xMin = s.x;
                if (s.x > xMax) xMax = s.x;
                if (s.y < yMin) yMin = s.y;
                if (s.y > yMax) yMax = s.y;
                any = true;
            }
        }

        if (!any) return false;
        rect = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        return true;
    }
}
