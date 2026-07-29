using System;

// The plain attack timing: fire the moment the unit is allowed to. The FSM only runs this while it's already
// facing the target and inside AttackRange, so this just triggers and lets the cooldown gate the rest. Facing/
// aim is the FSM's job (it faces before firing); range and the actual hit live on the skill.
[Serializable]
public class SimpleAttack : IAttackPlan
{
    public void Tick(AIContext ctx)
    {
        if (ctx.controller.CanAttack) ctx.controller.Attack();
    }
}
