using UnityEngine;

// ONE BLOW — a swing, a thrust, a wave of wind. It plays its own clip and lands on that clip's hit frame, and
// what it does at that moment is all a subclass has to say.
//
// THE SLOT DECIDES WHAT IT IS. Slot Attack and this IS the character's attack: press J and it swings. Slot None
// and no button reaches it — it is a step for a ComboAttack to throw, or an enemy's weapon that its AI throws.
// One class either way, because a blow is the same blow whoever pulls the trigger; a second class for the
// buttonless version would be the same shape maths kept in two places, drifting apart.
//
// THE CLIP IS THE TIMING, stated once, here. How long the character is committed is the clip's own length and
// when the blow lands is its hit frame — read off the art rather than authored twice. A heavier finisher is a
// longer drawing, not a number somebody has to keep in step with one.
//
// NO COOLDOWN OF ITS OWN while it is an attack: it waits on the unit's attack recovery — the clip's length plus
// the unit's Recovery, scaled by attack speed, exactly as every attack in the game always has. A wait declared
// here as well would be a second opinion about the same gap, and the slower would win in silence. On a skill
// button it is that skill's cooldown that paces it instead, and this stays zero.
//
// THE ARMING is what lets five blows share one animation without all five landing on it. A blow only answers a
// hit frame it was armed for, and it is armed by the same call that started it.
public abstract class AttackAbility : CharacterSkill
{
    [Tooltip("Which animation this blow swings. Its length is how long the character is committed, and its " +
             "hit frame is the moment the blow lands.")]
    [SerializeField] AnimAction anim = AnimAction.Attack;

    [Header("Step-in")]
    [Tooltip("How far the blow carries the character forward, in world units, along the way it is aimed. " +
             "0 = it swings where it stands. A step into a wall covers less, the same as a dash does.")]
    [SerializeField, Min(0f)] float lunge;

    [Tooltip("How much OF THE SWING the step takes, as a share of the clip rather than a number of seconds. " +
             "The clip already says how long the blow lasts, so this keeps its place in the drawing at any " +
             "attack speed and on any weapon — 0.35 is 'over the first third of it', whatever that is worth " +
             "in seconds today. Nothing to do while the distance is 0.")]
    [SerializeField, Range(0.05f, 1f)] float lungeShare = 0.35f;

    // Armed by whoever threw it, spent by the hit frame. Without it a blow would land on ANY play of its clip —
    // including one some other system started — and a clip cut short before its hit frame would leave the blow
    // owed, to be paid by whatever plays that clip next.
    bool _armed;

    bool _warned;   // one line per component, not one per press

    public AnimAction Anim => anim;

    void OnEnable()
    {
        if (Animator != null) Animator.Hit += OnHit;
    }

    // Disarmed as well as unsubscribed: a body put away mid-swing and pooled back out must not still owe a hit.
    void OnDisable()
    {
        if (Animator != null) Animator.Hit -= OnHit;
        _armed = false;
    }

    protected virtual void Start()
    {
        if (Animator == null)
            Debug.LogError($"[{GetType().Name}] no {nameof(UnitAnimator)} under the owner — this blow will " +
                           "never land.", this);
    }

    // THROW IT: spend the attack, play the clip, wait for the hit frame. The one path in, whether the press came
    // from the attack button, from a combo running its next step, or from an enemy's AI — so a blow cannot
    // behave differently depending on who asked for it.
    //
    // Returns false when it could not go — still recovering from the last blow — and nothing was spent, so the
    // press can be offered again. MCInput holds one for a moment and retries, which is what lets a player swing
    // slightly early and still get a clean string.
    public bool Swing()
    {
        if (Owner == null || Animator == null) return false;

        // A CLIP THAT IS NOT AUTHORED MEASURES ZERO, and a zero-length swing is not a fast attack — it is a free
        // one. Nothing commits the unit, the whole recovery collapses to the bare Recovery, and no hit frame
        // ever arrives, so the blow costs nothing and lands nothing. Refuse it and name the clip: that is an
        // authoring hole, and it should read as one instead of as a combo that hits like a machine gun.
        float length = ClipTime(anim);
        if (length <= 0f)
        {
            if (!_warned)
            {
                _warned = true;
                Debug.LogError($"[{GetType().Name}] this character's anim set has no '{anim}' clip — the blow " +
                               "cannot swing. Author the clip, or point this at one that exists.", this);
            }
            return false;
        }

        // Committed the way whoever asked commits things — Kind, not a decision made here. Pressed on the
        // attack button it is an attack and pays the attack recovery; thrown as a step of a combo it is still
        // the attack, because the combo is; sat on a skill button it commits as a skill and is paced by that
        // skill's own cooldown. The length already carries attack speed (ClipTime), so the drawing and the
        // lock always agree.
        if (!Owner.Commit(length, Kind)) return false;

        // AFTER THE COMMIT, NEVER BEFORE IT: a blow that was refused must leave the character exactly where it
        // found them, and a step already taken is not something a refusal can give back.
        //
        // ALONG THE AIM, not along where the feet are being steered: this is the blow moving, and the blow goes
        // where it was pointed. The share is of `length`, which already carries attack speed — so a quicker
        // swing covers the same ground in less time rather than covering less of it.
        if (lunge > 0f) Owner.Glide(Owner.FacingDir, lunge, length * lungeShare);

        _armed = true;
        PlayAnim(anim, Owner.AttackRate);
        return true;
    }

    protected override bool Run() => Swing();

    void OnHit(AnimAction action)
    {
        if (!_armed || action != anim) return;
        _armed = false;
        Land();
    }

    // What this blow actually does at the moment it connects.
    protected abstract void Land();
}
