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
public abstract class Projectile : MonoBehaviour
{
    public abstract void Launch(in Shot shot);

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
