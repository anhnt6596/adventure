using System;

// The plain attack timing: throw whatever sits in this creature's Attack slot, the moment it is allowed to. The
// FSM only runs this while it is already facing the target and inside AttackRange, so this just pulls the
// trigger and lets the attack's own recovery gate the rest. Facing/aim is the FSM's job; range and the actual
// hit live on the ability.
//
// BY SLOT, not by component type, and that is the point: a monster presses the same buttons a player does, so
// the day a creature swings a combo or casts a skill, this line does not change.
[Serializable]
public class SimpleAttack : IAttackPlan
{
    public void Tick(AIContext ctx)
    {
        var attack = ctx.Ability(AbilitySlot.Attack);
        if (attack != null) attack.TryUse();
    }
}
