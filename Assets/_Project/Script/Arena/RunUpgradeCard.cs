using UnityEngine;

// One card the player may be offered when they level up inside an arena. An asset of its own.
//
// ONE FILE PER CARD, and the reason is references rather than tidiness. Cards point at each other — a card
// that needs another first, a rule that holds one back — and a row in a list has no name to point at but its
// position, which moves the moment somebody inserts a row above it. An asset has an identity that survives
// being reordered, renamed and moved.
//
// THERE IS NO DECK LIST TO KEEP. A Config, so ConfigRegistry collects every one of these by itself — dropping
// a new card in the project IS adding it to the game. A hand-maintained list would be a second place to
// register a card and the first thing anybody forgets.
//
// WHICH CARDS EXIST IS THE PLAYER'S, NOT THE ARENA'S. What can turn up is what this SAVE has unlocked (see
// CardLibrary) — an arena is a place, and what the player has learned to find in one is a fact about the
// player. Hanging the pool off the arena would mean unlocking a card by walking into the right room.
//
// TWO WAYS TO SAY WHAT IT DOES, and they are for two different things:
//
//   buffs  — the character's own numbers, through the RUN's layer (RunStats). Its multipliers COMPOUND, which
//            the world's stat system deliberately does not do, and that difference is the whole reason a run
//            has a layer of its own rather than borrowing the tree's.
//   effect — anything that is not a plain stat: a skill's own tunable ("+2 dash distance"), an unlock, and
//            whatever kinds arrive later. The same IUpgradeEffect the character's tree uses, so those cards
//            cost no new code at all.
//
// Most cards fill in one or the other. A card may do both — a heavier swing that is also slower to come back
// is one idea in two places — and nothing here minds.
//
// IT CARRIES AN ID THOUGH NOTHING READS ONE YET. Availability rules are coming — plenty of cards should not
// turn up in the first minute — and every one of them has to name a card. Adding the field now costs a line;
// adding it after fifty cards are authored means going back and naming fifty of them, which is exactly the
// job nobody does properly.
//
// A CARD NEVER SHOWS ITS NUMBERS. It says which way it pushes — "+ attack" — and no more. The player is meant
// to feel a build running away with itself rather than do arithmetic in the three seconds a horde allows, and
// a card whose size is a mystery is still worth reading on the tenth run. It also means retuning a card can
// never make its own text a lie.
[CreateAssetMenu(menuName = "Arena/Upgrade Card")]
public class RunUpgradeCard : Config
{
    // The id comes from Config: type one and never change it. It is what the SAVE writes down when this card
    // is unlocked, and what a rule or another card will point at — a renamed id is a save entry that no longer
    // matches anything, silently. Empty falls back to the asset's file name, which is a fine default and a
    // poor promise, because renaming the file then moves the id with it.

    [Tooltip("Available from the very first run, with nothing to do to earn it. Untick for a card that has to " +
             "be earned — CardLibrary remembers which of those this save has, and a run never sees the rest.")]
    public bool unlockedByDefault = true;

    [Tooltip("What the player sees at the top of the card. Short — it is read in a second, under pressure.")]
    public string title = "";

    [Tooltip("Optional line under the numbers. Flavour, or a rule the numbers cannot say. Leave empty and the " +
             "card is just its effect.")]
    [TextArea(1, 3)] public string flavour = "";

    [Tooltip("How often this comes up relative to the other cards. 0 takes it out of the deck without " +
             "deleting it.")]
    [Min(0f)] public float weight = 1f;

    [Tooltip("How many times one run may take this. 0 = no limit. Use it for a card that would be absurd " +
             "stacked, and leave it at 0 for the plain stat cards — taking +10% attack five times is a build.")]
    [Min(0)] public int maxPerRun;

    [Tooltip("Plain stat changes, through the run's own layer. Compound multiplies stack up: taking a x1.25 " +
             "attack card four times is 2.44x, not 2x.")]
    public RunBuff[] buffs = System.Array.Empty<RunBuff>();

    [Tooltip("For anything that is NOT a plain stat — a skill's own number, an unlock. Same effect kinds the " +
             "character's upgrade tree uses. Leave empty on an ordinary stat card.")]
    [SerializeReference] public IUpgradeEffect effect;

    [Tooltip("The line shown for that effect, written by hand — for example: + dash distance\n\n" +
             "By hand because cards hide their numbers (see RunBuff.Describe), and the tree's own wording is " +
             "built to show them. Nothing can drift here: there is no figure on it to disagree with.")]
    public string effectLabel = "";

    // What the player reads before taking it: which way each thing moves, and NOT by how much. Retune a card
    // and this line stays true — there is no figure on it to go stale, which is the one good thing about
    // saying less.
    public string Describe()
    {
        var lines = new System.Collections.Generic.List<string>();

        if (buffs != null)
            foreach (var buff in buffs)
                if (!string.IsNullOrEmpty(buff.stat)) lines.Add(buff.Describe());

        if (effect != null && !string.IsNullOrWhiteSpace(effectLabel)) lines.Add(effectLabel);

        return string.Join("\n", lines);
    }
}
