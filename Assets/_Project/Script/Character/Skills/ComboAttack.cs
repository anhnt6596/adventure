using UnityEngine;

// A STRING OF BLOWS ON ONE BUTTON. Each press throws the next one; stop pressing for a moment and the string is
// over, so the next press starts from the top again. Bound to the Attack slot, this is what "attacking" means
// for a character that has one — there is no second kind of attack underneath it.
//
// IT IS A LIST OF ORDINARY SKILLS, dragged in, and one press throws exactly ONE of them. Any skill will do — a
// blow, a throw, a dash to end the string on — because a step is just a skill that nothing else is pressing
// (CharacterSkill.Trigger). A blow that does two things, like a sweep that also sends a wave out, is one skill
// that does two things (see WaveAttack), not two entries fired together: the player made one press, and what
// came of it is that skill's business, not this list's.
//
// Nothing about the steps is restated here. Which clip one swings, how long it commits the character, when it
// lands and what it lands with all live on the step itself, so a step is a reference and nothing else.
// Reordering the string is dragging the list.
//
// THE PACE IS THE UNIT'S, AND IT IS THE SAME FOR EVERY STEP. A combo sits in the Attack slot, so it commits as
// an attack — and it hands that down: a step runs with the combo's ActionKind, not with whatever it would have
// been on its own button. A dash used as a step is therefore paced like a swing, gap and all, without touching
// the six-second cooldown the same dash has when you press its own key.
public class ComboAttack : CharacterSkill
{
    [Tooltip("The string, in order. One press throws one of these; after the last, the next press starts again " +
             "at the first. Put them on this body with slot None, so nothing but this can throw them.")]
    [SerializeField] CharacterSkill[] steps;

    [Tooltip("Seconds of NOT attacking that end the string, counted from the moment the character was free to " +
             "swing again rather than from the press — otherwise a slow blow would time itself out while it " +
             "was still playing.")]
    [SerializeField, Min(0f)] float resetAfter = 2f;

    public const string ResetAfter = "reset";

    Stat _resetAfter;

    int _next;        // which step the NEXT press throws
    float _resetAt;   // when the string lapses, in Time.time

    protected override void Awake()
    {
        base.Awake();
        _resetAfter = Tunable(ResetAfter, resetAfter);
    }

    void Start()
    {
        if (steps == null || steps.Length == 0)
            Debug.LogError($"[{nameof(ComboAttack)}] no steps — this character's attack button does nothing.", this);
    }

    protected override bool Run()
    {
        if (Owner == null || steps == null || steps.Length == 0) return false;

        // Asked once, for the string rather than per step: every step commits as an attack below, so they all
        // wait on the same recovery and a mash cannot pour the whole combo out at once.
        if (!Owner.CanAttack) return false;

        // Checked at the press rather than counted down in an Update: a string that has lapsed costs nothing
        // until somebody swings again, and this is scaled time, so a paused game does not eat the window.
        if (Time.time >= _resetAt) _next = 0;

        var step = steps[_next];
        // Kind and not the step's own: while it is in this list it is part of the attack, whatever it is.
        if (step == null || !step.Trigger(Kind)) return false;   // refused: still recovering, MCInput holds the press

        _next = (_next + 1) % steps.Length;
        _resetAt = Time.time + Owner.AttackReadyIn + Mathf.Max(0f, _resetAfter.Value);
        return true;
    }
}
