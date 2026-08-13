// Presses a skill button. Thin like AttackCommand, and for the same reason: the binding layer says WHEN, the
// skill says whether it can and what happens — a command that checked the cooldown would be a second place
// that has to agree about it.
public class SkillCommand : ICharacterCommand
{
    readonly CharacterSkill _skill;

    public SkillCommand(CharacterSkill skill) => _skill = skill;

    public bool Execute() => _skill.TryUse();
}
