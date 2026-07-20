using Core.UI;
using UnityEngine;
using UnityEngine.UIElements;

// The in-game HUD, shown when the game starts. First panel: the main character's inventory, top-right.
// UXML/USS live next to this file and MUST be named GameHUD.* (the registry matches by file name).
//
// Data is pushed via SetInventory, not injected: the UISystem lives in the App scope and cannot
// resolve GameScope services, so GameUI (which is GameScope-injected) hands the inventory in.
public class GameHUD : UIView
{
    public override UILayer DefaultLayer => UILayer.HUD;

    Inventory _inv;
    Label _capacity;
    VisualElement _list;
    bool _expanded = true;

    public GameHUD(VisualElement root) : base(root)
    {
        _capacity = root.Q<Label>("capacity-label");
        _list = root.Q<VisualElement>("item-list");
        root.Q<Button>("capacity-button")?.RegisterCallback<ClickEvent>(_ => Toggle());
    }

    public void SetInventory(Inventory inv)
    {
        if (_inv != null) _inv.Changed -= Refresh;
        _inv = inv;
        Subscribe();
        Refresh();
    }

    public override void OnShow() { Subscribe(); Refresh(); }
    public override void OnHide() { if (_inv != null) _inv.Changed -= Refresh; }

    void Subscribe()
    {
        if (_inv == null) return;
        _inv.Changed -= Refresh;   // idempotent - never double-subscribe
        _inv.Changed += Refresh;
    }

    void Toggle()
    {
        _expanded = !_expanded;
        if (_list != null) _list.style.display = _expanded ? DisplayStyle.Flex : DisplayStyle.None;
    }

    void Refresh()
    {
        if (_inv == null) return;
        if (_capacity != null) _capacity.text = $"{_inv.Total}/{_inv.Capacity}";
        if (_list == null) return;

        _list.Clear();
        foreach (var kv in _inv.Counts)
        {
            if (kv.Value <= 0) continue;

            var row = new VisualElement();
            row.AddToClassList("item-row");

            var count = new Label(kv.Value.ToString());
            count.AddToClassList("item-count");

            var icon = new Image { sprite = kv.Key.icon };
            icon.AddToClassList("item-icon");

            row.Add(count);
            row.Add(icon);
            _list.Add(row);
        }
    }
}
