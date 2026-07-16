public class AttackCommand : ICharacterCommand
{
    readonly Character _character;

    public AttackCommand(Character character) => _character = character;

    public void Execute() => _character.Attack();
}
