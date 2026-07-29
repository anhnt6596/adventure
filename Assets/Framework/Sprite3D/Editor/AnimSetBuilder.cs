using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Builds a CharacterAnimSet out of the legacy AnimationClips a character was authored with, so the move off
// AnimatorControllers costs no re-authoring. A clip's real content was only ever an ordered sprite list plus
// an OnHit event; this reads exactly that back out and drops the state machine on the floor.
//
// Clips must be named "{Action}_{Direction}" — Idle_Right, Attack_Up, Move_DownLeft. Direction names are the
// screen sectors: Up, UpRight, Right, DownRight, Down, DownLeft, Left, UpLeft. On a mirrored set the
// left-hand clips are ignored (that is the point of mirroring); anything unmatched is reported, never
// silently dropped.
public static class AnimSetBuilder
{
    [MenuItem("Assets/Sprite3D/Build Character Anim Set", true)]
    static bool Validate() => FolderOf(Selection.activeObject) != null;

    [MenuItem("Assets/Sprite3D/Build Character Anim Set")]
    static void Build()
    {
        string folder = FolderOf(Selection.activeObject);
        if (folder == null) return;

        var clips = LoadClips(folder);
        if (clips.Count == 0)
        {
            Debug.LogError($"[AnimSetBuilder] no AnimationClips under {folder}.");
            return;
        }

        string path = Path.Combine(folder, Path.GetFileName(folder) + " AnimSet.asset").Replace('\\', '/');
        var set = AssetDatabase.LoadAssetAtPath<CharacterAnimSet>(path);
        bool fresh = set == null;
        if (fresh)
        {
            set = ScriptableObject.CreateInstance<CharacterAnimSet>();
            // Guess from what the folder actually holds: a character drawn with an Up pose is at least 4-way.
            bool hasPole = clips.ContainsKey((AnimAction.Idle, 0)) || clips.ContainsKey((AnimAction.Move, 0));
            set.dirs = hasPole ? DirCount.Four : DirCount.Two;

            // Off regardless of what the asset's own default is: an IMPORT must be lossless. Mirroring on
            // would throw away every left-hand clip in the folder without anyone seeing it, and whether a
            // given character mirrors at all is a per-character call. Bring it all in, then decide.
            set.mirror = false;
        }

        set.clips = BuildClips(set, clips);
        if (fresh) AssetDatabase.CreateAsset(set, path);
        else EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();

        Selection.activeObject = set;
        Debug.Log($"[AnimSetBuilder] {(fresh ? "created" : "rebuilt")} {path} — {set.dirs} dirs, mirror {set.mirror}, " +
                  $"{set.clips.Length} actions. Check dirs/mirror on the asset, then rebuild if you changed them.", set);
    }

    static CharacterAnimSet.Clip[] BuildClips(CharacterAnimSet set, Dictionary<(AnimAction, int), AnimationClip> clips)
    {
        var built = new List<CharacterAnimSet.Clip>();

        foreach (AnimAction action in System.Enum.GetValues(typeof(AnimAction)))
        {
            var slots = new CharacterAnimSet.DirFrames[set.AuthoredDirs];
            bool any = false;
            float fps = 0f;
            int hitFrame = -1;

            for (int sector = 0; sector < 8; sector++)
            {
                if (!clips.TryGetValue((action, sector), out var clip)) continue;

                // Route the clip through the set's own folding, so what lands in slot N is exactly what
                // Resolve will ask for at slot N. No second copy of the direction rules to drift.
                var (index, _) = set.Resolve(sector);
                if (index < 0 || index >= slots.Length) continue;

                var frames = SpritesOf(clip);
                if (frames.Length == 0)
                {
                    Debug.LogWarning($"[AnimSetBuilder] {clip.name} has no sprite curve — skipped.", clip);
                    continue;
                }

                if (slots[index] != null) continue;   // a mirrored set maps several sectors onto one slot
                slots[index] = new CharacterAnimSet.DirFrames { frames = frames };
                any = true;

                if (fps <= 0f) fps = FpsOf(clip, frames.Length);
                if (hitFrame < 0) hitFrame = HitFrameOf(clip, fps);
            }

            if (!any)
            {
                // A mirrored set authors the RIGHT half only, so left-only art folds to nothing at all. Say
                // that out loud — the silent version is a character that simply never draws this action.
                bool hadLeftOnly = false;
                for (int sector = 5; sector <= 7 && !hadLeftOnly; sector++) hadLeftOnly = clips.ContainsKey((action, sector));
                if (set.mirror && hadLeftOnly)
                    Debug.LogError($"[AnimSetBuilder] {action}: only left-hand clips found and mirror is on. " +
                                   "Mirroring always sources the RIGHT side — flip the art and rename, or turn mirror off.");
                continue;
            }

            for (int i = 0; i < slots.Length; i++)
                if (slots[i] == null)
                    Debug.LogWarning($"[AnimSetBuilder] {action}: nothing for slot {i} ({set.SlotLabel(i)}) — that facing will not draw.");

            built.Add(new CharacterAnimSet.Clip
            {
                action = action,
                fps = fps > 0f ? fps : 6f,
                loop = action != AnimAction.Attack,
                hitFrame = action == AnimAction.Attack ? hitFrame : -1,
                dirs = slots,
            });
        }

        return built.ToArray();
    }

    // The sprite keyframes, in time order. This is the whole payload of a flipbook clip.
    static Sprite[] SpritesOf(AnimationClip clip)
    {
        foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            if (binding.propertyName != "m_Sprite") continue;

            var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            var frames = new List<Sprite>(keys.Length);
            foreach (var key in keys)
                if (key.value is Sprite sprite) frames.Add(sprite);
            return frames.ToArray();
        }
        return new Sprite[0];
    }

    // Prefer the real key spacing: clip.frameRate is the authoring grid, which is not always the rate the
    // sprites were actually placed on.
    static float FpsOf(AnimationClip clip, int frameCount)
    {
        foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            if (binding.propertyName != "m_Sprite") continue;

            var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            if (keys.Length >= 2)
            {
                float step = keys[1].time - keys[0].time;
                if (step > 1e-5f) return Mathf.Round(1f / step * 1000f) / 1000f;
            }
        }
        return clip.frameRate > 0f ? clip.frameRate : 6f;
    }

    static int HitFrameOf(AnimationClip clip, float fps)
    {
        foreach (var evt in AnimationUtility.GetAnimationEvents(clip))
            if (evt.functionName == "OnHit") return Mathf.RoundToInt(evt.time * fps);
        return -1;
    }

    // "Attack_DownLeft" -> (Attack, sector 5). Returns false for anything that does not parse, so a stray
    // clip is reported rather than quietly folded into the wrong slot.
    static readonly Dictionary<string, int> Sectors = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase)
    {
        { "Up", 0 }, { "UpRight", 1 }, { "Right", 2 }, { "DownRight", 3 },
        { "Down", 4 }, { "DownLeft", 5 }, { "Left", 6 }, { "UpLeft", 7 },
    };

    static Dictionary<(AnimAction, int), AnimationClip> LoadClips(string folder)
    {
        var found = new Dictionary<(AnimAction, int), AnimationClip>();

        foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) continue;

            int split = clip.name.IndexOf('_');
            if (split <= 0
                || !System.Enum.TryParse(clip.name.Substring(0, split), true, out AnimAction action)
                || !Sectors.TryGetValue(clip.name.Substring(split + 1), out int sector))
            {
                Debug.LogWarning($"[AnimSetBuilder] {clip.name} does not parse as Action_Direction — skipped.", clip);
                continue;
            }

            found[(action, sector)] = clip;
        }

        return found;
    }

    static string FolderOf(Object obj)
    {
        if (obj == null) return null;
        string path = AssetDatabase.GetAssetPath(obj);
        return !string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path) ? path : null;
    }
}
