using System.Collections.Generic;
using UnityEngine;
using Lean.Pool;

// Everything a caster decides at the moment it lets go, in one value. Passed by `in` and never kept: a
// projectile copies out what it needs in Launch, because these are the numbers AS THEY WERE WHEN FIRED — a
// buff landing mid-flight must not bend a knife already in the air, the same rule the dash reads its distance
// under.
//
// WHY THE CASTER OWNS THESE AND NOT THE PREFAB. Speed, range and damage are what an upgrade node moves, and a
// node addresses a skill, not a prefab lying in a folder. Left on the projectile they would be one number
// shared by every caster that ever throws it, and no amount of tree would reach them.
public readonly struct Shot
{
    public readonly Vector3 Direction;   // flattened and normalised here, so nothing downstream repeats it
    public readonly int Team;            // the caster's — Overlap spares it, so no friendly fire
    public readonly float Damage;
    public readonly float Speed;
    public readonly float Range;
    public readonly float Knockback;
    public readonly Component Source;    // the caster, so a victim can hit back at whoever threw this

    public Shot(Vector3 direction, int team, float damage, float speed, float range, float knockback,
                Component source)
    {
        direction.y = 0f;
        Direction = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.forward;
        Team = team;
        Damage = damage;
        Speed = speed;
        Range = range;
        Knockback = knockback;
        Source = source;
    }

    // The same shot aimed somewhere else. What a fan is made of: one centre shot and copies of it turned off
    // the middle, so every knife in the spray carries identical numbers by construction rather than by the
    // caller remembering to pass the same ones each time round the loop.
    public Shot Turned(float degrees)
        => new Shot(Quaternion.Euler(0f, degrees, 0f) * Direction, Team, Damage, Speed, Range, Knockback, Source);
}

// Anything a skill or an attack can spawn and let go of. The base carries no flight and no hit: a soul-fire
// seeks and bursts, a knife runs straight and is stopped by whatever survives it, and a shared Update that
// tried to serve both would be a switch over which projectile this is.
//
// What it does own is the ONE contract — hand it a Shot and it is on its way — so the thing that spawns it
// never needs to know which projectile it is holding. That is what makes ProjectileSkill work for all of them.
//
// POOLED, ALWAYS: Launch must (re)set every field it reads, because this object has flown before.
//
// THE ONE THING EVERY PROJECTILE SHARES BESIDES THAT is meeting another one. Two shots crossing in the air is
// not a property of knives or of waves, it is a property of shots — so the rule lives here, once, rather than
// in each flight rewritten slightly differently.
public abstract class Projectile : MonoBehaviour
{
    public abstract void Launch(in Shot shot);

    // Whose it is. On the base because the clash below needs it and every Shot carries one — three private
    // copies of the same field was three chances for one of them to be read after it went stale.
    public int Team { get; protected set; }

    // ---- meeting another shot ------------------------------------------------------------------------
    //
    // BEING STOPPED BY A BODY AND BEING STOPPED BY A SHOT ARE TWO DIFFERENT QUESTIONS, so this is its own tick
    // rather than something read off the flight. A blade of pressure that bursts on the first man it reaches can
    // still be the kind of thing no arrow turns aside, and that combination is a real weapon — one you answer by
    // moving rather than by shooting back.
    //
    // WHAT IT DOES NOT DECIDE is whether this shot cancels OTHERS: erasing what it meets costs it nothing, and
    // an unblockable shot ploughing through a volley is the whole point of being unblockable. So an untickeded
    // one still clears the air ahead of it — it simply cannot be cleared.
    [Tooltip("Can be taken out of the air — by an opposing shot, or by a blade swung through it. Untick for " +
             "something that answers to bodies alone: it still erases what it meets, it just cannot be erased.")]
    [SerializeField] bool canBeBlocked = true;

    // Asked of the shot, not of the prefab: a flame already bursting has nothing left to cancel, and cancelling
    // it twice would restart the explosion. Subclasses with a moment like that say so here.
    protected virtual bool InFlight => true;

    bool Blockable => canBeBlocked && InFlight;

    // Does this shot's shape cover that point? Asked of each side in turn rather than summing two radii,
    // because these are not all circles — a wave is a long thin box, and a radius wide enough to be fair to its
    // width would catch shots passing well clear of it.
    protected abstract bool Reaches(Vector3 point);

    // Taken out of the air. Virtual so a shot with something to play on the way out can say so.
    protected virtual void Cancel() => LeanPool.Despawn(gameObject);

    // Everything in the air right now. A plain list, walked in full: shots number in the handful even in a
    // fight, and a spatial index for a handful costs more to keep than it saves. CombatWorld is a hash because
    // it holds every body in the world; this does not.
    static readonly List<Projectile> Live = new List<Projectile>();

    protected virtual void OnEnable() => Live.Add(this);
    protected virtual void OnDisable() => Live.Remove(this);

    // AFTER EVERYTHING HAS MOVED. Flights run in Update, so testing there would judge some pairs on this
    // frame's positions and some on last frame's, and which is which would depend on script order.
    void LateUpdate()
    {
        // Downwards, because cancelling removes from the list: everything below the cursor keeps its index.
        for (int i = Live.Count - 1; i >= 0 && i < Live.Count; i--)
        {
            var other = Live[i];
            if (other == null || other == this || other.Team == Team) continue;

            bool mine = Blockable, theirs = other.Blockable;
            if (!mine && !theirs) continue;   // two that carry have nothing to settle

            // EITHER SIDE REACHING IS A MEETING. Two shots of very different size — a knife and a wave — would
            // otherwise pass or clash depending on which of them was asked, and that is not a thing the player
            // could ever predict.
            Vector3 here = transform.position, there = other.transform.position;
            if (!Reaches(there) && !other.Reaches(here)) continue;

            if (theirs) other.Cancel();
            if (mine) { Cancel(); return; }   // gone; there is nothing left to meet
        }
    }

    // ---- swatting shots out of the air ---------------------------------------------------------------
    //
    // A BLADE IS AN OBSTACLE TOO. What cancels a shot is being met by something solid enough to spend it, and a
    // swing is exactly that — so a sword can bat a fireball down, and it is the same rule as two shots meeting,
    // asked of a shape that belongs to a body instead of to another shot. Which is why this lives here beside
    // that rule rather than growing a second, subtly different one inside the attack.
    //
    // The same "either side reaching" test: a shot standing inside the swing is caught, and so is a wave whose
    // own shape lies across the swing even though its centre is somewhere else.
    public static void CancelIn(Vector3 centre, float radius, int team)
    {
        for (int i = Live.Count - 1; i >= 0 && i < Live.Count; i--)
        {
            var shot = Live[i];
            if (!Catchable(shot, team)) continue;

            Vector3 d = shot.transform.position - centre;
            d.y = 0f;
            if (d.sqrMagnitude <= radius * radius || shot.Reaches(centre)) shot.Cancel();
        }
    }

    public static void CancelIn(Vector3 centre, Vector3 forward, float halfWidth, float halfLength, int team)
    {
        forward.y = 0f;
        Vector3 fwd = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.right;
        Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);

        for (int i = Live.Count - 1; i >= 0 && i < Live.Count; i--)
        {
            var shot = Live[i];
            if (!Catchable(shot, team)) continue;

            Vector3 d = shot.transform.position - centre;
            d.y = 0f;
            bool inside = Mathf.Abs(Vector3.Dot(d, fwd)) <= halfLength
                       && Mathf.Abs(Vector3.Dot(d, right)) <= halfWidth;

            if (inside || shot.Reaches(centre)) shot.Cancel();
        }
    }

    static bool Catchable(Projectile shot, int team) => shot != null && shot.Blockable && shot.Team != team;

    // One press, one or several shots, spaced about the direction the centre one is aimed at. Static and here
    // rather than on whatever spawns it, because more than one thing does: a skill fires a fan on the frame
    // its clip connects, and so does a plain attack that is not a skill at all. Two copies of this loop would
    // be two places for the parity of an even count to be got wrong.
    //
    // `spread` is the gap between NEIGHBOURS, so an odd count lands one straight down the middle and an even
    // one straddles it — no branch on parity, the first offset is simply half the total width back.
    public static void Fan(Projectile prefab, Vector3 from, in Shot centre, int count, float spread)
    {
        if (prefab == null) return;

        // ON THE GROUND, whatever height it was thrown from. A projectile's transform is its place on the
        // BOARD — the whole game is played on the XZ plane, every hit test flattens Y before it measures, and
        // a shot spawned at hand height would be a body floating a foot above the floor it is fighting on.
        // How high the thing LOOKS is the art child's business and no part of where it is.
        from.y = 0f;

        int shots = Mathf.Max(1, count);
        float offset = -spread * (shots - 1) * 0.5f;

        for (int i = 0; i < shots; i++)
            LeanPool.Spawn(prefab, from, Quaternion.identity).Launch(centre.Turned(offset + spread * i));
    }
}
