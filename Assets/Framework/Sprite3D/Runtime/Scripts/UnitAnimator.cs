using System;
using UnityEngine;

// Plays a sprite character's frames straight out of a CharacterAnimSet — no AnimatorController, no state
// machine, no triggers. The playhead belongs to the ACTION; the direction is resolved fresh every frame and
// only decides which list of sprites that playhead reads. So a unit that turns, or a camera that orbits, in
// the middle of a swing shows the same frame of the same swing from another side — nothing restarts, and
// nothing is stuck facing the way the action started.
//
// The hit frame is an index into the clip rather than an AnimationEvent, so it cannot drift out of sync with
// the frames it is supposed to land on.
public class UnitAnimator : MonoBehaviour
{
    [SerializeField] CharacterAnimSet set;
    [SerializeField] SpriteRenderer target;    // the sprite being driven; empty = the one on this object
    [SerializeField] Transform scaleNode;      // flipped on X to serve mirrored directions

    // Raised as the playhead crosses the current clip's hit frame — once per play of a one-shot clip.
    public event Action Hit;

    // Scales playback without touching the clip. An attack sets this to the unit's attack rate so the swing
    // stretches with its lock window; everything else runs at 1.
    public float PlaybackSpeed { get; set; } = 1f;

    public AnimAction Action => _action;

    // How long an action runs at 1x, in seconds — frames / fps. This is the authored truth about how long the
    // action TAKES, which is why combat can read it instead of repeating the number by hand in a config.
    // Directions are assumed to be the same length; the first populated one answers for all of them.
    public float LengthOf(AnimAction action)
    {
        var clip = set != null ? set.Get(action) : null;
        if (clip == null || clip.dirs == null || clip.fps <= 0f) return 0f;

        for (int i = 0; i < clip.dirs.Length; i++)
        {
            var frames = clip.dirs[i]?.frames;
            if (frames != null && frames.Length > 0) return frames.Length / clip.fps;
        }
        return 0f;
    }

    AnimAction _action = AnimAction.Idle;
    CharacterAnimSet.Clip _clip;
    Sprite[] _frames;      // the resolved direction's list; re-resolved whenever the direction changes
    float _cursor;         // playhead, measured in FRAMES so the hit index is a plain comparison
    int _frame = -1;
    int _dir8;
    int _resolved = -1;    // last authored slot applied, so an unchanged direction costs nothing
    bool _hitFired;
    Vector3 _oriScale;

    void Awake()
    {
        if (target == null) target = GetComponent<SpriteRenderer>();
        if (target == null) target = GetComponentInChildren<SpriteRenderer>();
        if (scaleNode != null) _oriScale = scaleNode.localScale;
        _clip = set != null ? set.Get(_action) : null;
        Resolve(true);
    }

    void Start()
    {
        if (set == null)
            Debug.LogError($"[{nameof(UnitAnimator)}] no {nameof(CharacterAnimSet)} assigned — this unit will never animate.", this);
        else if (target == null)
            Debug.LogError($"[{nameof(UnitAnimator)}] no {nameof(SpriteRenderer)} to drive.", this);

        // A leftover Animator writes m_Sprite in the animation phase, which runs AFTER Update — so it silently
        // overwrites every frame this component sets, and its own State/Dir parameters are no longer driven by
        // anything. The unit then looks frozen on one pose in one direction with no error anywhere. Say it.
        var stale = GetComponent<Animator>();
        if (stale != null && stale.enabled && stale.runtimeAnimatorController != null)
            Debug.LogError($"[{nameof(UnitAnimator)}] an {nameof(Animator)} with a controller is still on this object. " +
                           "It overwrites the sprite after Update and will win — remove the component.", this);
    }

    // Switch action. A looping action already playing is left alone — calling this every frame from a view is
    // the normal case, and restarting the walk cycle each frame would freeze it on frame 0. A one-shot always
    // restarts, because asking for an attack again means another attack.
    public void Play(AnimAction action)
    {
        var clip = set != null ? set.Get(action) : null;
        bool same = _action == action && _clip != null;
        _action = action;
        _clip = clip;

        if (same && clip != null && clip.loop) return;

        _cursor = 0f;
        _frame = -1;
        _hitFired = false;
        Resolve(true);
    }

    // The direction the unit reads as ON SCREEN, as an 8-sector index (0 Up, then clockwise). Cheap to call
    // every frame — it only does work when the sector actually changes.
    public void SetDir(int dir8)
    {
        if (_dir8 == dir8) return;
        _dir8 = dir8;
        Resolve(false);
    }

    void Resolve(bool force)
    {
        if (set == null || _clip == null || _clip.dirs == null) { _frames = null; return; }

        var (index, flip) = set.Resolve(_dir8);
        if (!force && index == _resolved)
        {
            ApplyFlip(flip);   // the slot can stay put while the flip flips (a mirrored set does exactly that)
            return;
        }

        _resolved = index;
        _frames = index >= 0 && index < _clip.dirs.Length ? _clip.dirs[index]?.frames : null;
        ApplyFlip(flip);
        if (_frame >= 0) Draw(_frame);   // keep showing the same beat of the action, just from the new side
    }

    void ApplyFlip(bool flip)
    {
        if (scaleNode == null) return;
        scaleNode.localScale = new Vector3(_oriScale.x * (flip ? -1f : 1f), _oriScale.y, _oriScale.z);
    }

    void Update()
    {
        if (_clip == null || _frames == null || _frames.Length == 0) return;

        _cursor += Time.deltaTime * Mathf.Max(0f, _clip.fps) * Mathf.Max(0f, PlaybackSpeed);

        int len = _frames.Length;
        int raw = Mathf.FloorToInt(_cursor);
        int f = _clip.loop ? ((raw % len) + len) % len : Mathf.Min(raw, len - 1);

        // One-shot only: on a loop the playhead would sweep past the hit index on every pass.
        if (!_hitFired && !_clip.loop && _clip.hitFrame >= 0 && f >= _clip.hitFrame)
        {
            _hitFired = true;
            Hit?.Invoke();
        }

        if (f != _frame) Draw(f);

        // A one-shot that has run PAST its last frame hands itself back to the resting action, rather than
        // holding the final pose until some caller happens to ask for something else. Compared against raw,
        // not f, so the last frame still gets shown for its full duration first.
        if (!_clip.loop && raw >= len && _action != set.rest) Play(set.rest);
    }

    void Draw(int frame)
    {
        _frame = frame;
        if (target == null || _frames == null || _frames.Length == 0) return;
        target.sprite = _frames[Mathf.Clamp(frame, 0, _frames.Length - 1)];
    }
}
