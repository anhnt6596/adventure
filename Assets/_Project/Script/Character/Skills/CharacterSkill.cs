using UnityEngine;

// One of a character's two skills, living on that character's own prefab — the same shape ShapeAttack has, and
// for the same reason. Docs/DESIGN.md says a character IS stats + attack + skill, and only the last two are
// behaviour rather than numbers; which skill a character carries is therefore part of the body, not a field
// somebody sets on a config.
//
// THE BASE COOLDOWN IS AUTHORED HERE, NOT AS A CHARACTER STAT. A dash balanced at eight seconds is eight
// seconds whoever casts it — what differs between two characters is which skill they have, and that is this
// prefab. As a stat it would also need one entry per slot per skill: a number every character's config has to
// carry for a skill it may not even own, which is the same enumeration DESIGN.md rejects for gear rungs.
//
// WHAT IS A STAT IS THE HASTE — see StatId.Skill1Haste. Upgrades and gear move that; nothing moves the base.
//
// NO DEPENDENCY INJECTION. The skill reads what it needs off its owner, the way ShapeAttack reads AttackPower,
// so the same component works on a body that has ICharacterStats and on one that does not. An enemy given a
// dash simply has no haste and uses the authored cooldown.
public abstract class CharacterSkill : MonoBehaviour
{
    public enum Slot { One = 1, Two = 2 }

    [Tooltip("Which of the character's two skill buttons this one answers to.")]
    [SerializeField] Slot slot = Slot.One;

    [Tooltip("WHAT this skill is, for display — the icon is looked up by (character id, this key), the same " +
             "way an upgrade node's is. Nothing readable is stored on the skill itself.")]
    [SerializeField] string key = "";

    [Tooltip("Seconds before it can be used again, BEFORE haste. The skill's own number — a character's haste " +
             "shortens it, nothing replaces it.")]
    [SerializeField, Min(0f)] float cooldown = 8f;

    protected DynamicUnit Owner { get; private set; }

    ICharacterStats _stats;   // null on anything that is not a main character — then haste is simply 0
    float _readyAt;

    public Slot Which => slot;
    public string Key => key;

    // THE SKILL DRIVES ITS OWN ANIMATION, and this class does not have an opinion about it. There is no "the
    // clip this skill plays", because that is not a shape skills have: some play nothing, some run several
    // clips in order, some choose by what is happening at the time. Any single field or property here would
    // be right for exactly one of those and a lie for the rest.
    //
    // The bargain that makes it work is in UnitView: while a skill is HOLDING the unit, the view stops
    // driving the animator entirely, so nothing overwrites what the skill asked for. A skill that holds owns
    // the animation for that whole window; one that does not hold leaves the walk cycle alone, which is what
    // a skill with no animation of its own wants anyway.
    protected UnitAnimator Animator { get; private set; }

    protected void PlayAnim(AnimAction action, float speed = 1f)
    {
        if (Animator == null) return;
        Animator.PlaybackSpeed = speed;
        Animator.Play(action);
    }

    protected virtual void Awake()
    {
        Owner = GetComponentInParent<DynamicUnit>();
        if (Owner == null)
        {
            Debug.LogError($"[{GetType().Name}] no {nameof(DynamicUnit)} above it — a skill belongs on the body " +
                           "that casts it.", this);
            return;
        }

        // Read, never written: a skill may ask what the character is, and may not decide it.
        _stats = (Owner as MCController)?.Stats;

        // Found the same way UnitView finds it, rather than dragged in: there is one animator under a body,
        // and a serialized field would be one more thing to forget on every skill added to every prefab.
        Animator = Owner.GetComponentInChildren<UnitAnimator>();
    }

    IStat Haste => _stats?.Get(slot == Slot.One ? StatId.Skill1Haste : StatId.Skill2Haste);

    // Ability haste rather than a percentage off:
    //
    //     cooldown = base / (1 + haste / 100)
    //
    // 100 haste halves it, and it approaches zero without ever arriving — which is why it needs no cap. A
    // straight "-X%" does: the points just below 100% are worth unbounded amounts, so it has to be clamped at
    // some arbitrary number and every point after that is worth nothing at all.
    //
    // Negative haste is allowed and lengthens the wait, because a debuff that does that is a real thing to
    // want. The divisor is floored so that -100 or worse cannot divide by zero or run the cooldown backwards.
    public float Cooldown
    {
        get
        {
            float scale = 1f + (Haste?.Value ?? 0f) * 0.01f;
            return cooldown / Mathf.Max(0.05f, scale);
        }
    }

    // Off Time.time rather than counted down in an Update: a skill that is doing nothing should cost nothing,
    // and this is scaled time, so a paused game pauses the wait with it.
    public float Remaining => Mathf.Max(0f, _readyAt - Time.time);
    public bool Ready => Time.time >= _readyAt;

    // What the button reads to draw a sweep. Full at the moment of use, empty when it is ready again.
    public float CooldownFraction
    {
        get
        {
            float total = Cooldown;
            return total > 0f ? Mathf.Clamp01(Remaining / total) : 0f;
        }
    }

    public bool TryUse()
    {
        if (!Ready || Owner == null) return false;

        // Not while another skill is playing out. A swing, by contrast, is no obstacle — pressing this
        // cancels it, which is the one asymmetry in the rules. See ActionKind.
        if (!Owner.CanUseSkill) return false;

        if (!Run()) return false;

        // Charged only once the skill agreed to happen. A press that could do nothing must not eat the
        // cooldown — the player reads that as the game having swallowed the button.
        _readyAt = Time.time + Cooldown;
        return true;
    }

    // Do the thing. Return false if it could not start at all, and it stays off cooldown.
    protected abstract bool Run();
}
