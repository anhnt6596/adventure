using System;
using UnityEngine;

// BY WHAT THE MOVEMENT IS, not by which button triggered it. Naming these Skill1/Skill2 after the slots would
// tie the art to the binding: the same DashSkill component sits in slot one on one character and slot two on
// another, and the same lunge would have to be authored twice under two names. A skill says which of these it
// plays (see CharacterSkill.Anim), so two skills can share a clip and a skill can reuse Attack outright.
//
// It grows once per KIND of movement, which is slow — Docs/DESIGN.md wants the count of attacks and skills
// small — and a character without that skill simply has no clip for it rather than an empty slot to fill.
//
// APPEND ONLY, WITH EXPLICIT NUMBERS. These are serialized inside every CharacterAnimSet asset, so inserting
// a value in the middle silently slides every authored clip onto a different action.
public enum AnimAction { Idle = 0, Move = 1, Attack = 2, Dash = 3 }

// How many screen directions the art actually covers.
public enum DirCount { Two = 2, Four = 4, Eight = 8 }

// Everything a sprite character's animation IS: per action, an ordered list of frames per authored direction,
// plus the rate and the frame an attack connects on. There is deliberately no state machine here. Direction is
// a LOOKUP performed at draw time, not part of a state's identity — which is the whole point: turning, or
// orbiting the camera, mid-action swaps which list is read while the playhead keeps running. An
// AnimatorController cannot do that, because there "attack facing north-east" is a different state from
// "attack facing east", and moving between states means re-entering one.
[CreateAssetMenu(menuName = "Sprite3D/Character Anim Set")]
public class CharacterAnimSet : ScriptableObject
{
    [Serializable]
    public class DirFrames
    {
        public Sprite[] frames;
    }

    [Serializable]
    public class Clip
    {
        public AnimAction action;
        public float fps = 6f;
        public bool loop = true;
        public int hitFrame = -1;   // frame an attack lands on; -1 = never raises Hit. One-shot clips only.
        public DirFrames[] dirs;    // one entry per AUTHORED direction, indexed by what Resolve returns
    }

    [Tooltip("How many screen directions the art covers.")]
    public DirCount dirs = DirCount.Two;

    [Tooltip("Left-facing directions reuse the RIGHT-facing art, flipped on X. Halves what has to be drawn: 2 -> 1, 4 -> 3, 8 -> 5.")]
    public bool mirror = true;

    [Tooltip("What a finished one-shot returns to. Loops never fall back.")]
    public AnimAction rest = AnimAction.Idle;

    public Clip[] clips;

    // WHICH SIDE IS AUTHORED IS NOT A CHOICE: it is always the RIGHT half, sectors 0..4 (Up, UpRight, Right,
    // DownRight, Down). That is what makes an eight-way mirrored set's slot index BE its screen sector — slot
    // 2 is Right, full stop, in every set in the project. Let a set author the left half instead and slot 2
    // would mean Right in one asset and Left in another, and the 8 - sector fold would have to invert with it.
    // Art that arrives facing left gets flipped once at import; it does not get a runtime flag.

    public Clip Get(AnimAction action)
    {
        if (clips == null) return null;
        for (int i = 0; i < clips.Length; i++)
            if (clips[i] != null && clips[i].action == action) return clips[i];
        return null;
    }

    // How many direction entries a clip in this set must author.
    public int AuthoredDirs => mirror
        ? (dirs == DirCount.Two ? 1 : dirs == DirCount.Four ? 3 : 5)
        : (int)dirs;

    static readonly string[] SectorNames = { "Up", "UpRight", "Right", "DownRight", "Down", "DownLeft", "Left", "UpLeft" };

    // What a slot index means for THIS set's dirs/mirror — the label the builder and any warning should use,
    // so nobody has to reverse-engineer Resolve to find out which pose slot 1 wants.
    public string SlotLabel(int slot)
    {
        switch (dirs)
        {
            case DirCount.Eight:
                return slot >= 0 && slot < 8 ? SectorNames[slot] : "?";
            case DirCount.Four:
                if (slot == 0) return "Up";
                if (slot == 2) return "Down";
                if (slot == 1) return mirror ? "Right (mirrored for Left)" : "Right";
                return mirror ? "?" : "Left";
            default:
                if (slot == 0) return mirror ? "Right (mirrored for Left)" : "Right";
                return mirror ? "?" : "Left";
        }
    }

    // Fold a SCREEN 8-sector onto the directions this set actually authored, and say whether to flip X.
    // The sector order is the one UnitView feeds in: 0 Up, 1 UpRight, 2 Right, 3 DownRight, 4 Down,
    // 5 DownLeft, 6 Left, 7 UpLeft. Sectors 5..7 are the left-hand half — the only ones a mirror can serve,
    // and each has a right-hand twin at 8 - sector (5<->3, 6<->2, 7<->1).
    public (int index, bool flip) Resolve(float screenDeg)
    {
        float deg = Mathf.Repeat(screenDeg, 360f);

        switch (dirs)
        {
            // Only a side pose exists, so every heading resolves to a side — the half of the circle it is in.
            case DirCount.Two:
            {
                bool left = deg > 180f;
                if (mirror) return (0, left);                 // slots: 0 Side
                return (left ? 1 : 0, false);                 // slots: 0 Right, 1 Left
            }

            // Four EQUAL quadrants, each centred on its axis: Up -45..45, Right 45..135, and so on. This is
            // why the angle is folded here rather than an 8-sector index: sector edges sit at 22.5 + n*45, so
            // no grouping of whole sectors can make a 90 degree band centred on Up. Grouping them 1/3/1/3 —
            // the obvious way — is what gives the side pose three times the reach of Up and Down.
            case DirCount.Four:
            {
                int q = Mathf.FloorToInt(Mathf.Repeat(deg + 45f, 360f) / 90f);   // 0 Up, 1 Right, 2 Down, 3 Left
                if (q == 0) return (0, false);
                if (q == 2) return (2, false);
                if (mirror) return (1, q == 3);               // slots: 0 Up, 1 Side, 2 Down
                return (q == 3 ? 3 : 1, false);               // slots: 0 Up, 1 Right, 2 Down, 3 Left
            }

            // Eight poses match the sector grid exactly, so this is the one case where sectors ARE the answer.
            default:
            {
                int s = ViewAngleUtil.GetViewType8(deg);
                bool left = s >= 5;
                bool flip = mirror && left;
                return (flip ? 8 - s : s, flip);              // slots ARE sectors: 0..7, or 0..4 when mirrored
            }
        }
    }
}
