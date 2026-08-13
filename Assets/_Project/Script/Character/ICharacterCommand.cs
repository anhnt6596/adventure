// One press, one thing the character might do.
//
// IT SAYS WHETHER IT HAPPENED, and that is the whole reason the return value exists: a press that could not
// run — swinging already, still on cooldown — is the one the input layer has to remember and offer again a
// moment later. A void Execute leaves the caller unable to tell a shot from a blank, and buffering is exactly
// the job of telling those apart.
public interface ICharacterCommand
{
    bool Execute();
}
