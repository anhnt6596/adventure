public class AttackCommand : ICharacterCommand
{
    readonly MC _character;

    public AttackCommand(MC character) => _character = character;

    public void Execute() => _character.Attack();
}
