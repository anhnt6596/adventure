// Never starts a fight — returns no target, so a monster with this only enters combat through EnemyAI's
// reactive "got hit" path. Timid creatures like MewFrog use it.
public class PassiveAggro : IAggro
{
    public IDamageable Detect(AIContext ctx) => null;
}
