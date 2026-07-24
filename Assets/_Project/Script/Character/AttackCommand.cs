public class AttackCommand : ICharacterCommand
{
    readonly MCController _character;

    public AttackCommand(MCController character) => _character = character;

    public void Execute() => _character.Attack();
}
