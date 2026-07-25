// The plain attack timing: fire whenever the swing window is free. The FSM only runs this while it's already
// facing the target and inside AttackRange, so this just triggers and lets the cooldown gate the rest. Facing/
// aim is the FSM's job (it faces before firing); range and the actual hit live on the skill.
public class SimpleAttack : IAttackPlan
{
    public void Tick(AIContext ctx)
    {
        if (!ctx.controller.IsBusy) ctx.controller.Attack();
    }
}
