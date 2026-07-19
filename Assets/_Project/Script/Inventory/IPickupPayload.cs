// What a pickup gives. One payload per pickable — a pickup grants exactly one kind of thing. The payload
// knows which store it targets, so a picker never branches on type: it asks CanDeliver (any room?) before
// collecting, and Deliver takes what FITS — returning true when fully consumed, false if some is left.
public interface IPickupPayload
{
    bool CanDeliver(IPickupReceiver receiver);
    bool Deliver(IPickupReceiver receiver);
}
