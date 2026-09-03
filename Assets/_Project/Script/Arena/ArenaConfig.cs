using UnityEngine;
using UnityEngine.Serialization;

// One arena: a place you fight in, and everything true about fighting there — which map it is, how long a run
// lasts, and (when they land) which monsters live there and what surviving it pays.
//
// THE ARENA IS THE THING, THE GATE IS A DOOR. A gate carries no rules of its own; one arena has one gate, and
// that gate only says where it goes. The alternative — rules on the gate, geometry on the map — leaves the
// monsters of a forest decided by something standing in a field somewhere else, which is backwards.
//
// A RUN IS ALWAYS FROM SCRATCH, AND AN ARENA ALWAYS FORGETS. Walk in and you are level nothing with an empty
// bag and nothing built; walk out and the place is put back exactly as authored — trees you felled, walls you
// raised, monsters you cleared, all of it comes back for the next run. This is not a policy anybody enforces:
// the map prefab is destroyed on the way out and instantiated again on the way in, and everything the run
// owned lived in a scope that no longer exists. The only thing that leaves is the reward.
//
// WHICH IS WHY NOTHING IN AN ARENA MAY WRITE TO THE SAVE. A PayGate authored in here would remember what was
// fed into it across runs — see the build spots task in Docs/GATE_RUN.md: they need a run-scoped twin, not
// the saved one.
//
// SO NOTHING MARKS A MAP AS "AN ARENA MAP" EITHER. A map is geometry; what makes a place a run is that
// ArenaRunner opened one. A flag on the map would be a second answer to that, free to disagree with the
// first the day anybody loads that map another way.
[CreateAssetMenu(menuName = "Arena/Arena")]
public class ArenaConfig : Config
{
    [Tooltip("How hard this place is, for the player to read before walking in. A label, not a lookup: what " +
             "actually lives here and how fast it comes is authored below, on this asset.")]
    [Min(0)] public int rank;

    [Header("The place")]
    [Tooltip("Map prefab under Resources/Maps, by id — the same id MapService takes. Name arenas after the " +
             "PLACE (Arena_Forest).\n\n" +
             "The map must contain no Portal and no ArenaGate: an arena is left by clearing it or by dying, " +
             "and by nothing else. ArenaRunner says so at load if it finds one.")]
    [FormerlySerializedAs("mapId")]
    public string mapId;

    [Tooltip("Which spawn point in that map the player arrives at.")]
    [FormerlySerializedAs("gateIndex")]
    [Min(0)] public int spawnIndex;

    [Header("Monsters")]
    [Tooltip("Every wave this arena throws, authored one row at a time: which day, between which hours, " +
             "what, and how many. Order does not matter — several waves may overlap.")]
    public ArenaWave[] waves = System.Array.Empty<ArenaWave>();

    [Tooltip("Ceiling on how many are alive at once. Not a difficulty knob — a machine one: past it a wave " +
             "holds off instead of grinding the frame rate down, and resumes as soon as there is room.")]
    [Min(1)] public int maxAlive = 80;

    [Tooltip("How far from the PLAYER monsters appear, in world units: X = nearest, Y = farthest.\n\n" +
             "Set X just past the corner of the screen so nothing pops into view, and Y a few units beyond " +
             "so a wave does not arrive on one perfect circle. Around the character, not around the camera: " +
             "the character is what the monsters are coming for, and the shot is fixed anyway (the wheel was " +
             "taken away for exactly this).")]
    public Vector2 spawnRing = new Vector2(18f, 24f);

    [Header("Levelling")]
    // WHICH CARDS can turn up is NOT here: it is what the player has unlocked (CardLibrary). An arena decides
    // how fast you climb, not what you are offered when you do — the first is a property of the place, the
    // second a property of the person.
    [Tooltip("Experience the NEXT level costs, read at the current level (x = level). A curve rather than a " +
             "formula, so a short test arena and a long one can climb completely differently without code.")]
    public AnimationCurve expToNext = AnimationCurve.Linear(1f, 10f, 20f, 200f);

    [Tooltip("How many cards a level-up offers. Three is the usual: enough to be a choice, few enough to " +
             "read while a horde waits.")]
    [Min(1)] public int handSize = 3;

    [Header("The run")]
    [Tooltip("Days to survive to clear it. A day is one full cycle from the moment you walk in, not a " +
             "calendar day ending at midnight — see RunClock.")]
    [Min(1)] public int days = 1;

    [Tooltip("Seconds per in-game day in here. Defaults to the overworld's pace; an arena that wants longer " +
             "nights or a tighter run changes it.")]
    [Min(1f)] public float dayLengthSeconds = DayNightClock.DefaultDayLengthSeconds;

    [Tooltip("Hour of day a run starts on. Morning by default, so it opens with a daylight stretch to gather " +
             "and build in before the first night.")]
    [Range(0f, 24f)] public float startHour = 7f;
}

// One wave: a batch of one kind, on one day, arriving across one stretch of hours.
//
// AUTHORED, NOT DERIVED FROM A CURVE. A rate curve says "it gets busier" and leaves what actually happens to
// arithmetic nobody can picture; a wave says "on night two, thirty of these come between 19:00 and 22:00",
// which is a thing a designer can decide, read back, and change one number of. Difficulty here is a schedule
// somebody wrote, not a slope somebody tuned.
//
// A ROW IS ONE DAY. Wanting the same wave every night is several rows, and that is the honest cost of being
// able to make night four different from night three without touching anything else.
//
// THE COUNT ARRIVES SPREAD ACROSS THE WINDOW, not in a lump: a wave from 19:00 to 22:00 is three hours of
// pressure. Set both hours the same for a lump — an ambush at first light is a legitimate thing to author.
[System.Serializable]
public struct ArenaWave
{
    [Tooltip("Prefab + EnemyConfig id, the same id EnemySpawner takes.")]
    public string enemyId;

    [Tooltip("Which day of the run. 0 = the day you walk in.")]
    [Min(0)] public int day;

    [Tooltip("Hour the wave starts arriving. Keep the window inside one day — a run's day turns over at the " +
             "hour the arena opens on (startHour), not at midnight, so a wave straddling that hour stops.")]
    [Range(0f, 24f)] public float fromHour;

    [Tooltip("Hour it stops arriving. Same as From = the whole batch lands at once.")]
    [Range(0f, 24f)] public float toHour;

    [Tooltip("How many bodies this wave is worth, in total.")]
    [Min(1)] public int count;

    // Wraps past midnight, like every other window in this game (EnemyBrainConfig's waking hours, and the
    // arena's own night). A wave authored 22 -> 3 is one night, not twenty-three hours of daylight.
    public bool Contains(float hour)
    {
        // Same hour = a lump, and it drops the moment that hour arrives — so the window is "from then on",
        // and the count is what stops it rather than the clock.
        if (Mathf.Approximately(fromHour, toHour)) return hour >= fromHour;
        return toHour > fromHour ? hour >= fromHour && hour < toHour
                                 : hour >= fromHour || hour < toHour;
    }

    // Length of the window in hours, wrap included. Zero means "all at once".
    public float WindowHours => Mathf.Approximately(fromHour, toHour) ? 0f
                              : toHour > fromHour ? toHour - fromHour
                              : 24f - fromHour + toHour;
}
