// The plain attack timing: fire whenever the swing window is free. The FSM only runs this while the target is
// already inside AttackRange, so it just waits out the busy period and triggers again — repeating for as long
// as the target stays in range. Range and the actual hit live on the skill (IAttack); this only decides WHEN.
public class SimpleAttack : IAttackPlan
{
    public void Tick(AIContext ctx)
    {
        if (!ctx.controller.IsBusy) ctx.controller.Attack();
    }
}
