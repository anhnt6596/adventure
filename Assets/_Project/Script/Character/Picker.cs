using UnityEngine;
using VContainer;

// Grabs any Pickable within the character's pickup radius and hops it into the picker. Exposes the
// holder's stores as an IPickupReceiver, so a pickup's payload delivers itself into the right one and
// this stays generic (no branching on currency/resource/item).
[DisallowMultipleComponent]
public class Picker : MonoBehaviour, IPickupReceiver
{
    [SerializeField] float pickHeight = 1f;        // fly-in goal lifts off the ground — the picker's Y is 0
    [SerializeField] string inventoryId = "main_char";   // which store in the InventorySystem this picker fills

    ICharacterStats _stats;
    Inventory _inventory;

    public Inventory Inventory => _inventory;

    [Inject]
    public void Construct(ICharacterStats stats, IInventoryConfig inventoryConfig, InventorySystem inventories)
    {
        _stats = stats;
        _inventory = inventories.GetOrCreate(inventoryId, inventoryConfig);
    }

    void Start()
    {
        if (_stats == null || _inventory == null)   // needs ICharacterStats + IInventoryConfig + InventorySystem
            Debug.LogError($"[{nameof(Picker)}] not injected — add this GameObject to GameScope's Auto Inject list.", this);
    }

    void Update()
    {
        if (_stats == null) return;

        float r = _stats.PickupRadius;
        float r2 = r * r;
        Vector3 p = transform.position;

        var list = Pickable.Active;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var pk = list[i];
            if (pk == null || !pk.CanPick) continue;

            Vector3 d = pk.Position - p;
            d.y = 0f;
            if (d.x * d.x + d.z * d.z <= r2)
                pk.CollectTo(this, transform, pickHeight);   // pk gates on its payload; full backpack → stays
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (_stats == null) return;   // runtime only (radius comes from stats)
        Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.6f);
        float r = _stats.PickupRadius;
        Vector3 c = transform.position;
        const int seg = 32;
        Vector3 prev = c + new Vector3(r, 0, 0);
        for (int i = 1; i <= seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg;
            Vector3 next = c + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
#endif
}
