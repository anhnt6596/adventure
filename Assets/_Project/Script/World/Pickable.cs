using System.Collections.Generic;
using Lean.Pool;
using UnityEngine;

// A thing a Picker can grab when in range. Registers itself so pickers find it without scanning. While a
// piece is still flying out (FlyingPickup) it's not pickable; it turns pickable on landing.
//
// One pickable = one payload (IPickupPayload) — a pickup gives exactly one kind of thing. When the picker
// reaches it, the payload takes what FITS immediately. Whatever was taken spawns a throwaway PickupFlyVisual
// that hops into the picker (juice, even on a partial take). If the whole thing was taken the pickup is
// gone; if the backpack filled first, the remainder stays here on the ground.
[DisallowMultipleComponent]
public class Pickable : MonoBehaviour
{
    // Simple registry — a Picker iterates this instead of scanning the scene. Can graduate to a
    // DI-registered service (like CombatWorld) if pickups ever need spatial queries at scale.
    public static readonly List<Pickable> Active = new List<Pickable>();

    [SerializeField] PickupFlyVisual flyVisual;   // drag the fly-visual prefab (Billboard root + sprite child)

    public bool CanPick { get; private set; }
    public Vector3 Position => transform.position;
    public event System.Action<Pickable> Picked;

    IPickupPayload _payload;

    void Awake() => _payload = GetComponent<IPickupPayload>();

    void OnEnable()
    {
        CanPick = true;
        Active.Add(this);
    }

    void OnDisable() => Active.Remove(this);

    public void SetPickable(bool value) => CanPick = value;

    // Called by a Picker each frame it's in range. Delivers what fits, flies a visual for the taken part,
    // and vanishes only if fully taken — otherwise the remainder stays on the ground for later.
    public void CollectTo(IPickupReceiver receiver, Transform target, float height)
    {
        if (!CanPick) return;

        bool fullyTaken = true;
        if (_payload != null)
        {
            if (!_payload.CanDeliver(receiver)) return;   // no room → stay put
            fullyTaken = _payload.Deliver(receiver);       // take what fits (credits the inventory now)
        }

        SpawnFlyVisual(target, height);   // juice for what was taken (partial or full)
        Picked?.Invoke(this);

        if (!fullyTaken) return;          // backpack filled → the remainder stays on the ground

        CanPick = false;
        LeanPool.Despawn(gameObject);     // fully taken → recycle the piece (falls back to Destroy if unpooled)
    }

    void SpawnFlyVisual(Transform target, float height)
    {
        if (PickupFly.HasHandler) return;   // the HUD flies an icon to the UI instead
        if (flyVisual == null) return;
        var fly = LeanPool.Spawn(flyVisual, transform.position, transform.rotation);
        fly.Launch(target, height);
    }
}
