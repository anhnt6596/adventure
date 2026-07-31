using UnityEngine;

// A pickup that is EATEN on collection. Nothing carries food, so there is no store to put it in — what it
// fills is the picker's stomach, and the piece is gone the moment it is swallowed. See Docs/DECISIONS.md.
//
// The shape is `ResourcePayload`'s, deliberately, so a food drop is authored exactly like a wood drop: same
// Pickable, same FlyingPickup, same pooling. The one difference is which store it delivers into, which is
// the whole reason IPickupPayload exists — a Picker never branches on what it is picking up.
//
// ONE PICKUP IS ONE MEAL, and that is the one place this must NOT copy ResourcePayload. A resource may be
// taken in part because the bag filled up, and the bag then stays full until the player does something about
// it. A stomach empties by itself, every frame. Leave a half-eaten piece on the ground the way a resource
// does and a full character standing on it swallows one frame's worth of drain, over and over, for ever: a
// fountain of flying icons and a joint that never disappears. So the piece is always finished — Deliver
// returns true whatever happened, and overflow past the stomach's size is simply lost.
//
// A COMPLETELY FULL STOMACH LEAVES IT ON THE GROUND, untouched. Not a refusal the player has to resolve —
// no message, no choice — just food that waits. That is what lets food have a ceiling without a single pixel
// of UI. Anything short of full eats it, so the rule a player learns is "walking over food eats it, unless
// I am completely full" and never "why will it not pick this up".
//
// THERE IS NO ICON FIELD, on purpose. ResourcePayload takes its icon from the ResourceDef because that icon
// is SHARED — every prefab that drops wood has to fly the same picture into the same row. Food has no def,
// no row and no list: the thing flying to the stomach is this very piece, so its own SpriteRenderer is the
// only correct answer and a field beside it would just be a second copy to drift. It would also break the
// flight, which starts at the piece's on-screen size and shrinks — a different sprite would visibly swap
// the object at the moment it is picked up.
//
// No per-instance state, so nothing to reset for the pool.
[DisallowMultipleComponent]
public class FoodPayload : MonoBehaviour, IPickupPayload
{
    [Tooltip("Fullness this restores, in the same units as Hunger's max. Whatever will not fit is lost.")]
    [SerializeField, Min(1f)] float nourishment = 25f;

    public float Nourishment => nourishment;

    // Any room at all is enough. Only a stomach with none refuses.
    public bool CanDeliver(IPickupReceiver receiver)
    {
        var hunger = receiver.Hunger;
        return hunger != null && hunger.Max > 0f && !hunger.IsFull;
    }

    // Eats the piece and finishes it, whether or not all of it fitted — Eat clamps to what is left and the
    // rest is gone with the meal. Returning true is what despawns the piece; anything else brings the
    // per-frame trickle back.
    public bool Deliver(IPickupReceiver receiver)
    {
        var hunger = receiver.Hunger;
        if (hunger == null) return false;

        if (hunger.Eat(nourishment) > 0f)
        {
            var sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
                PickupFly.RequestFood(sr.bounds.center,                                      // the art's centre, not the ground pivot
                                      sr.sprite,
                                      Mathf.Max(sr.bounds.size.x, sr.bounds.size.y));
        }
        return true;
    }
}
