// A timid ranged frog: ambles around its spawn, ignores everyone until struck, then chases in a straight line
// and spits at range. Everything is the shared FSM — only the four strategies below make it a MewFrog. Its
// skill (a ranged SoulFireAttack on the prefab) decides the attack distance and the actual hit.
public class MewFrogAI : EnemyAI
{
    protected override AIStrategies Build() => new AIStrategies
    {
        Idle    = new WanderRoam(),       // stroll to a spot near spawn, rest, repeat
        Aggro   = new PassiveAggro(),     // never starts a fight — only retaliates
        Pursuit = new StraightPursuit(),  // dumb straight-line chase
        Attack  = new SimpleAttack(),     // fire whenever off-cooldown and in range
    };
}
