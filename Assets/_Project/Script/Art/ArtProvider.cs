using System.Collections.Generic;
using UnityEngine;

// Loads art out of Resources by convention, and remembers what it found.
//
// FOLDER LAYOUT. A Resources folder counts at any depth, so these are paths INSIDE whichever one holds them
// — stat icons currently live under Assets/_Project/Art/Icons/Resources/, which makes the path just
// "StatIcons/hp". The shape follows what the project already does for maps (Resources/Maps/{id}).
//
//     <anywhere>/Resources/Avatars/MC 1.png
//     <anywhere>/Resources/StatIcons/hp.png
//
// Only things looked up BY NAME belong here. Anything a UXML, a prefab or a serialized field points at is
// already packed because something points at it — the heart and stomach on the HUD are referenced straight
// from GameHUD.uss and need no Resources folder at all. Everything under one, by contrast, ships whether it
// is used or not.
//
// CACHED, MISSES INCLUDED. Resources.Load is not free and these are asked for on every HUD rebind and every
// time the upgrade popup opens. A miss is cached as null too, so a piece of art nobody has drawn yet costs
// one failed load for the session rather than one per ask — the trade being that dropping the file in while
// playing will not be picked up until the next run.
public class ArtProvider : IArtProvider
{
    const string AvatarFolder = "Avatars";

    // Public because the tree editor tells the author where it looked when it found nothing, and a second
    // copy of the folder name is a message that goes stale the first time this moves.
    public const string UpgradeFolder = "StatIcons";

    readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();
    readonly HashSet<string> _warned = new HashSet<string>();

    public Sprite Avatar(string characterId)
        => string.IsNullOrEmpty(characterId) ? null : Get($"{AvatarFolder}/{characterId}");

    // Per-character first, then a shared fallback:
    //
    //     Resources/Upgrades/MC 1/body      <- this character's own
    //     Resources/Upgrades/body           <- everyone's, unless overridden above
    //
    // Most trees will want the same picture for "one more heart"; the few that want their own get it by
    // dropping a file in, with nothing to configure and nothing to switch on.
    public Sprite UpgradeIcon(string characterId, string nodeKey)
    {
        if (string.IsNullOrEmpty(nodeKey)) return null;

        if (!string.IsNullOrEmpty(characterId))
        {
            var own = Load($"{UpgradeFolder}/{characterId}/{nodeKey}");
            if (own != null) return own;
        }
        return Get($"{UpgradeFolder}/{nodeKey}");
    }

    public Sprite Get(string path)
    {
        var sprite = Load(path);

        // Once per path, not once per ask: a missing picture should say so, and then be quiet about it.
        if (sprite == null && !string.IsNullOrEmpty(path) && _warned.Add(path))
            Debug.LogWarning($"[{nameof(ArtProvider)}] no sprite at Resources/{path} — whatever wanted it " +
                             "draws nothing. Either the art is not in a Resources folder, or the name does " +
                             "not match the key asking for it.");

        return sprite;
    }

    // The silent one. A per-character override missing is the NORMAL case — it is the fallback doing its
    // job — so probing for one must not warn, or the console fills with notices about art nobody wanted.
    Sprite Load(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (_cache.TryGetValue(path, out var cached)) return cached;

        var sprite = Resources.Load<Sprite>(path);
        _cache[path] = sprite;
        return sprite;
    }
}
