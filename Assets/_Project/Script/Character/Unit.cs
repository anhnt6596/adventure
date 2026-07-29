// Everything hittable in the world: an id (Identifiable), a side (Team), and its combat stats behind
// IDamageableConfig. A Unit is STATIC by nature — it just stands there and can be hit. DynamicUnit adds the
// move/attack loop; a plain Unit (a Prop) runs no Update. Damageable/Dropable read HP/team/drops off this, so
// nothing carries a serialized config.
public abstract class Unit : Identifiable
{
    public virtual int Team => Teams.Universal;                 // see Teams — 0 means "nobody set one", and everything can hit it
    public virtual IDamageableConfig DamageableConfig => null;  // HP/team + (props) drops; null = inert
}
