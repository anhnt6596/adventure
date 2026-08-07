using UnityEngine;

// Art fetched by NAMING CONVENTION rather than by a reference sitting on a config.
//
// The alternative — a Sprite field on MainCharStatsConfig, another on ResourceDef, another on whatever comes
// next — spreads the same question across every config in the project and makes each one carry a lump of
// view. A character config should say how fast the character moves, not what its face looks like: one is the
// rules, the other is presentation, and they change for different reasons and by different people.
//
// So the deal is: if you can name the thing, you can find its art. Nothing has to be wired, a new character
// needs no config edit to get a portrait, and the day art moves it moves in ONE place.
//
// The cost, stated plainly: a missing file is a runtime miss instead of an empty slot visible in the
// inspector. That is why the implementation logs the path it looked for rather than just returning null.
public interface IArtProvider
{
    // The face of a main character, by the same id everything else uses to ask for one.
    Sprite Avatar(string characterId);

    // An upgrade node's icon, by the pair that identifies it. A node key only has to be unique inside its
    // own tree, so the character is half of the question — and that is what lets every character have a
    // "body" node without them all having to look the same.
    Sprite UpgradeIcon(string characterId, string nodeKey);

    // A skill button's icon, by the same pair and the same fallback. The basic attack asks for one too, under
    // the key "attack" — it is a button on the same bar, so it is a picture looked up the same way rather
    // than a special case with its own field somewhere.
    Sprite SkillIcon(string characterId, string skillKey);

    // Anything else, by its path under a Resources folder. The typed helpers above are the ones to prefer —
    // this exists so a new kind of art does not have to wait for a new method.
    Sprite Get(string path);
}
