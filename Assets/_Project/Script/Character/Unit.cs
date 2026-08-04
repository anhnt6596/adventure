// Everything hittable in the world: an id (Identifiable), a side (Team), and its combat stats behind
// IDamageableConfig. A Unit is STATIC by nature — it just stands there and can be hit. DynamicUnit adds the
// move/attack loop; a plain Unit (a Prop) runs no Update. Damageable/Dropable read HP/team/drops off this, so
// nothing carries a serialized config.
public abstract class Unit : Identifiable
{
    public virtual int Team => Teams.Universal;                 // see Teams — 0 means "nobody set one", and everything can hit it
    public virtual IDamageableConfig DamageableConfig => null;  // HP/team + (props) drops; null = inert

    // A LIVE maximum, for a unit whose HP can be buffed. Null means "the config's number is the number",
    // which is what a prop or an enemy wants — they are defined by their kind and nothing modifies them.
    // Only the main character fills this in today; the seam exists so the day a player debuff has to land on
    // an enemy, that enemy overrides this and Damageable does not change at all.
    public virtual IStat MaxHpStat => null;
}
