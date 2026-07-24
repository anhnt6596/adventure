// Everything hittable in the world: an id (Identifiable), a side (Team), and its combat stats behind
// IDamageableConfig. A Unit is STATIC by nature — it just stands there and can be hit. DynamicUnit adds the
// move/attack loop; a plain Unit (a Prop) runs no Update. Damageable/Dropable read HP/team/drops off this, so
// nothing carries a serialized config.
public abstract class Unit : Identifiable
{
    public virtual int Team => 0;                               // 0 neutral / 1 player / 2 enemy+environment
    public virtual IDamageableConfig DamageableConfig => null;  // HP/hit-radius/team + (props) drops; null = inert
}
