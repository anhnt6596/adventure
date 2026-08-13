using UnityEngine;

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
}
