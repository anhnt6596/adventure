using System.Collections.Generic;
using UnityEngine;

// One of the things a character can DO, living on that character's own prefab. Docs/DESIGN.md says a character
// IS stats + attack + skill, and only the last two are behaviour rather than numbers; which of them a character
// carries is therefore part of the body, not a field somebody sets on a config.
//
// The attack is one of these too, and so is the dash. What separates them is the slot they answer to — see
// AbilitySlot — not a different base class: a swing and a lunge both play a clip, both hold the unit, both take
// upgrades on their own tunables, and a combo has to be able to drive either as one of its steps.
//
// THE BASE COOLDOWN IS AUTHORED HERE, NOT AS A CHARACTER STAT. A dash balanced at eight seconds is eight
// seconds whoever casts it — what differs between two characters is which skill they have, and that is this
// prefab. As a stat it would also need one entry per slot per skill: a number every character's config has to
// carry for a skill it may not even own, which is the same enumeration DESIGN.md rejects for gear rungs.
//
// BOTH LAYERS MOVE, AND THEY ARE DIFFERENT THINGS. The base is this skill's own tunable, so a node addressed
// to this skill takes seconds off the number authored here; StatId.Skill1Haste is the character's, and scales
// whatever that came to. Flat first, haste after — see Cooldown.
//
// NO DEPENDENCY INJECTION. The skill reads what it needs off its owner, the way ShapeAttack reads AttackPower,
// so the same component works on a body that has ICharacterStats and on one that does not. An enemy given a
// dash simply has no haste and uses the authored cooldown.
public abstract class CharacterSkill : MonoBehaviour
{
    [Tooltip("Which button runs this. None = no button does — it is a piece of something else, like one step " +
             "of a combo, and only the ability driving it can fire it.")]
    [SerializeField] AbilitySlot slot = AbilitySlot.None;

    [Tooltip("WHAT this skill is, for display — the icon is looked up by (character id, this key), the same " +
             "way an upgrade node's is. Nothing readable is stored on the skill itself.")]
    [SerializeField] string key = "";

    [Tooltip("Seconds before it can be used again, BEFORE haste. The skill's own number — a character's haste " +
             "shortens it, nothing replaces it. Ignored while this commits as an ATTACK: an attack is paced by " +
             "the unit's own recovery, and a second wait here could only disagree with it.")]
    [SerializeField, Min(0f)] float cooldown = 8f;

    // Declared here rather than by each skill, because every skill has one: the name is the same on all of
    // them, so a node reads "cooldown" whatever it is addressed to.
    public const string CooldownTunable = "cooldown";

    [Tooltip("Starts shut, and stays shut until an upgrade node opens it (UnlockSkillEffect, by the Key " +
             "above). Off means the character has it from the first minute.")]
    [SerializeField] bool locked;

    protected DynamicUnit Owner { get; private set; }

    ICharacterStats _stats;   // null on anything that is not a main character — then haste is simply 0
    Stat _cooldown;
    float _readyAt;

    public AbilitySlot Which => slot;
    public string Key => key;

    // HOW THIS COMMITS THE UNIT while it plays, and it is deliberately NOT something each skill decides for
    // itself. It falls out of which button runs it: the attack button commits as an ATTACK, which a skill may
    // cut short; every other button commits as a SKILL, which nothing cuts. See ActionKind.
    //
    // A piece with no button — a step of a combo — INHERITS it from whatever threw it. That is the whole point:
    // a combo sits in the Attack slot, so a dash used as one of its steps is part of the attack while it is in
    // there, and is paced like one. The same dash on its own button is a skill again. Nothing about the dash
    // changed; what changed is who asked for it.
    public ActionKind Kind { get; private set; } = ActionKind.Skill;

    ActionKind KindOfSlot => slot == AbilitySlot.Attack ? ActionKind.Attack : ActionKind.Skill;

    // Whether the character actually has this yet. A skill that was never locked is simply always open; one
    // that was waits to be told, and is told again from scratch on every re-apply of the tree — so a respec
    // shuts it without anything having to remember that it had been opened.
    //
    // PUSHED IN rather than asked for: the skill would otherwise need the upgrade system, the character's id
    // and the node's id, which is three things a component on a prefab has no business knowing.
    public bool Unlocked => !locked || _unlocked;
    bool _unlocked;

    public void SetUnlocked(bool value) => _unlocked = value;

    // ---- the skill's own numbers -------------------------------------------------------------------
    //
    // A SKILL OWNS ITS TUNABLES; THEY ARE NOT CHARACTER STATS. A dash has a distance and a duration, a shout
    // has a radius — put those on the character and every character carries a number for a skill it may not
    // have, and the list grows with every skill ever written.
    //
    // It cannot be keyed by SLOT either, which is the trap worth naming: the same DashSkill sits in slot one
    // on one character and slot two on another, so a "Skill1Range" would mean something different per
    // character, and moving a skill between slots would silently detach its upgrades. Exactly why AnimAction
    // names what the movement IS rather than which button ran it.
    //
    // So the pair that addresses a tunable is (this skill's Key, the tunable's name) — the same Key its icon
    // and its unlock node already use. One name for one skill, everywhere.
    //
    // Same Stat class the character uses, so several nodes stack, a respec takes them off by source, and
    // nothing here re-implements arithmetic that already exists.
    readonly Dictionary<string, Stat> _tunables = new Dictionary<string, Stat>();

    // Declared in Awake, from whatever the inspector holds. The serialized field stays the BASE — it is what
    // the skill is worth with no upgrades on it.
    protected Stat Tunable(string name, float baseValue)
    {
        var stat = new Stat(baseValue);
        if (!string.IsNullOrEmpty(name)) _tunables[name] = stat;
        return stat;
    }

    // Null for a name this skill does not have — which is what a mistyped node comes down to, and why the
    // one applying them says so once rather than failing quietly. See PlayerSystem.
    public Stat Modifiable(string name)
        => !string.IsNullOrEmpty(name) && _tunables.TryGetValue(name, out var stat) ? stat : null;

    public void RemoveBySource(object source)
    {
        foreach (var stat in _tunables.Values) stat.RemoveBySource(source);
    }

    public void BeginBatch() { foreach (var stat in _tunables.Values) stat.BeginBatch(); }
    public void EndBatch()   { foreach (var stat in _tunables.Values) stat.EndBatch(); }

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
        // Before the guard below: a skill with nothing above it is broken, but a node still addresses this by
        // name, and a tunable that exists on some bodies and not others is worse than one that simply reads 8.
        _cooldown = Tunable(CooldownTunable, cooldown);

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

    // ONLY THE TWO SKILL SLOTS HAVE ONE. There is no attack haste because the attack already has a stat that
    // does that job — AttackSpeed scales the swing and the recovery together — and no dash haste because
    // nothing has asked for one; a tree that wants a quicker dash moves the dash's own `cooldown` tunable,
    // which is the per-skill route that already exists. A combo step (None) never waits on a cooldown at all.
    IStat Haste
    {
        get
        {
            if (_stats == null) return null;
            if (slot == AbilitySlot.Skill1) return _stats.Get(StatId.Skill1Haste);
            if (slot == AbilitySlot.Skill2) return _stats.Get(StatId.Skill2Haste);
            return null;
        }
    }

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
    //
    // The base is the tunable, so a flat "-1s" node is seconds off THIS number and haste then scales what is
    // left — a second of a slow skill and a second of a fast one are not worth the same, which is the point.
    // Floored at zero: the tunable can be driven negative, and a wait that runs backwards is not a shorter one.
    public float Cooldown
    {
        get
        {
            float scale = 1f + (Haste?.Value ?? 0f) * 0.01f;
            float @base = Mathf.Max(0f, _cooldown?.Value ?? cooldown);
            return @base / Mathf.Max(0.05f, scale);
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
        if (!Unlocked || Owner == null) return false;

        Kind = KindOfSlot;

        // A SKILL WAITS ON ITS COOLDOWN; AN ATTACK WAITS ON THE UNIT. Which one this is has already been decided
        // by the slot, so the wait follows from it instead of from a number somebody has to remember to zero.
        // The attack's own gate is inside DynamicUnit.Commit, and it is the same gate every attack passes.
        if (Kind == ActionKind.Skill && !Ready) return false;

        // Not while another skill is playing out. A swing, by contrast, is no obstacle — pressing this
        // cancels it, which is the one asymmetry in the rules. See ActionKind.
        if (!Owner.CanUseSkill) return false;

        if (!Run()) return false;

        // Charged only once the skill agreed to happen. A press that could do nothing must not eat the
        // cooldown — the player reads that as the game having swallowed the button. Nothing to charge for an
        // attack: the recovery it started IS its wait.
        if (Kind == ActionKind.Skill) _readyAt = Time.time + Cooldown;
        return true;
    }

    // RUN IT NOW, with somebody else owning the pacing. Same thing TryUse does, minus the cooldown: no wait is
    // checked and none is charged, because the caller is what decides when this happens — a combo running its
    // next step, and whatever else ever drives one skill from another.
    //
    // It is what makes any skill usable as a piece of something bigger. A string of blows, a string that ends in
    // a dash, a skill that fires another skill: none of those need a second kind of component, because the
    // difference between "a button runs it" and "something else runs it" is which of these two methods was
    // called — and, on the data side, whether it claimed a slot at all.
    //
    // Still refuses on its own terms: Run returns false when the thing simply cannot happen (mid-recovery, no
    // body to move), and that answer is passed straight back so the caller can decide what to do about it.
    public bool Trigger() => Trigger(KindOfSlot);

    // Run it as part of something bigger, committing the unit the way THAT thing commits it.
    public bool Trigger(ActionKind kind)
    {
        if (!Unlocked || Owner == null) return false;

        Kind = kind;
        return Run();
    }

    // Do the thing. Return false if it could not start at all, and it stays off cooldown.
    protected abstract bool Run();
}
