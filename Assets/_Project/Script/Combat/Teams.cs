// Who is on whose side. Every team number in the game comes from here — they used to be magic ints spread
// across eight files, which is how "props are team 3" and "enemies are team 2" quietly stopped agreeing with
// half the comments about them.
//
// A team answers TWO different questions and the whole design turns on keeping them apart:
//
//   "Can I hit this?"    — CombatWorld filters same-team targets out of every attack. Answered by team alone.
//   "Should I hunt this?" — AI target search. NOT the same question: a tree is on another team, which makes it
//                           hittable, and that is exactly why an axe works on it. It does not make it PREY.
//                           Ask Teams.IsPrey, never just "is it a different number to mine".
public static class Teams
{
    // Nobody IS this. It tags damage that belongs to no side — a trap, a fire, a falling rock — and CombatWorld
    // skips its team filter entirely for an attacker on 0, so everything in the blast takes it. A UNIT left on
    // 0 is therefore hittable by all comers including its own kind, which makes it a loud default for a team
    // somebody forgot to set rather than a quiet one.
    public const int Universal = 0;

    public const int Player = 1;

    // Peaceful animals. Their own side, so they don't hurt each other, and predators can hunt them.
    public const int Critter = 2;

    // Trees, rocks, chests. Deliberately far above every creature team so IsPrey is a threshold and not a list
    // anyone has to remember to extend — a new kind of scenery lands on the right side of it for free.
    public const int Resource = 10000;

    // Aggressive monsters take 3, 4, 5... — ONE PER KIND, which is what lets them fight each other and hunt
    // critters. There is no single "enemy" team any more; anything asking for one is a bug.
    public const int FirstMonster = 3;

    public static bool IsResource(int team) => team >= Resource;

    // Worth hunting: alive, on a side, and not scenery. What every AI target search must filter by.
    public static bool IsPrey(int team) => !IsResource(team);
}
