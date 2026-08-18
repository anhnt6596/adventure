// The attack button. It runs whatever ability claimed the Attack slot — a five-hit string, a single blow — and
// there is deliberately no fallback: a character with nothing in that slot cannot attack, and MCInput says so
// out loud rather than papering over it with a swing nobody authored.
public class AttackCommand : ICharacterCommand
{
    readonly CharacterSkill _ability;

    public AttackCommand(CharacterSkill ability) => _ability = ability;

    public bool Execute() => _ability != null && _ability.TryUse();
}
