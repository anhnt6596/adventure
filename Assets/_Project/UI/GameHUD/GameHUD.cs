using System.Collections.Generic;
using Core.UI;
using UnityEngine;
using UnityEngine.UIElements;

// The in-game HUD, shown when the game starts. First panel: the main character's inventory, top-right.
// A picked-up resource flies an icon from its world spot to its HUD slot (or the bag when the list is
// collapsed); the shown count only ticks up when the icon lands. The inventory itself is credited
// immediately - the fly is pure juice, so an interrupted animation never loses an item.
//
// UXML/USS live next to this file and MUST be named GameHUD.* (the registry matches by file name).
// Data is pushed via SetInventory (the App-scope UISystem cannot resolve GameScope services).
public class GameHUD : UIView
{
    public override UILayer DefaultLayer => UILayer.HUD;

    Inventory _inv;
    Label _capacity;
    VisualElement _list, _bag, _flyLayer;
    bool _expanded = true;

    readonly Dictionary<ResourceDef, int> _displayed = new();          // lags the inventory; catches up on land
    readonly Dictionary<ResourceDef, VisualElement> _rowIcon = new();  // fly target per resource

    // Fly icons are pooled and pre-added to the layer once, invisible. A pooled element is already in the
    // tree with resolved layout, so setting its position on reuse applies at once - no (0,0) flash that a
    // freshly-added element paints for one frame before its inline styles resolve.
    readonly Stack<Image> _flyPool = new();
    readonly List<Fly> _active = new();
    IVisualElementScheduledItem _flyTicker;

    class Fly
    {
        public Image icon;
        public ResourceDef def;
        public int amount;
        public Vector2 from;
        public float startSize;
        public float elapsed;
    }

    public GameHUD(VisualElement root) : base(root)
    {
        _capacity = root.Q<Label>("capacity-label");
        _list = root.Q<VisualElement>("item-list");
        _bag = root.Q<VisualElement>("bag-icon");
        _flyLayer = root.Q<VisualElement>("fly-layer");
        root.Q<Button>("capacity-button")?.RegisterCallback<ClickEvent>(_ => Toggle());
        WarmFlyPool(20);
    }

    public void SetInventory(Inventory inv)
    {
        Unsub();
        _inv = inv;
        SnapToActual();   // pre-existing items appear without a fly
        Sub();
        Refresh();
    }

    public override void OnShow() { Sub(); Refresh(); }
    public override void OnHide() { Unsub(); }

    void Sub()
    {
        if (_inv != null) { _inv.Changed -= Reconcile; _inv.Changed += Reconcile; }
        PickupFly.Requested -= OnPickup; PickupFly.Requested += OnPickup;
    }

    void Unsub()
    {
        if (_inv != null) _inv.Changed -= Reconcile;
        PickupFly.Requested -= OnPickup;
    }

    void Toggle()
    {
        _expanded = !_expanded;
        if (_list != null) _list.style.display = _expanded ? DisplayStyle.Flex : DisplayStyle.None;
    }

    void SnapToActual()
    {
        _displayed.Clear();
        if (_inv == null) return;
        foreach (var kv in _inv.Counts) if (kv.Value > 0) _displayed[kv.Key] = kv.Value;
    }

    // Inventory changed by something other than a pickup fly (crafting, load): snap down removals now;
    // additions are left for their flies to deliver on land.
    void Reconcile()
    {
        if (_inv == null) return;
        var keys = new List<ResourceDef>(_displayed.Keys);
        foreach (var def in keys)
        {
            int actual = _inv.Get(def);
            if (actual < _displayed[def])
            {
                if (actual <= 0) _displayed.Remove(def);
                else _displayed[def] = actual;
            }
        }
        Refresh();
    }

    const float EndSize = 86f;    // matches the .item-icon size the fly shrinks into
    const float FlyDur = 0.45f;

    void OnPickup(Vector3 worldPos, ResourceDef def, int amount, float worldHeight)
    {
        var cam = Camera.main;
        if (_flyLayer == null || Root.panel == null || cam == null) { Land(def, amount); return; }

        // Measure along the camera's up (screen-vertical), not world up: a tilted camera foreshortens world
        // Y, which under-read the piece's real on-screen height.
        Vector2 from = RuntimePanelUtils.CameraTransformWorldToPanel(Root.panel, worldPos, cam);
        Vector2 fromTop = RuntimePanelUtils.CameraTransformWorldToPanel(Root.panel, worldPos + cam.transform.up * worldHeight, cam);
        float startSize = Mathf.Clamp(Mathf.Abs(from.y - fromTop.y), EndSize, 1024f);   // on-screen size of the piece

        var icon = RentFlyIcon();
        icon.sprite = def.icon;
        Place(icon, from, startSize);

        _active.Add(new Fly { icon = icon, def = def, amount = amount, from = from, startSize = startSize });
        if (_flyTicker == null) _flyTicker = Root.schedule.Execute(StepFlies).Every(16);
        else _flyTicker.Resume();
    }

    void StepFlies(TimerState ts)
    {
        float dt = ts.deltaTime / 1000f;
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var f = _active[i];
            f.elapsed += dt;
            float t = Mathf.Clamp01(f.elapsed / FlyDur);
            float posE = 1f - (1f - t) * (1f - t);              // ease-out: leaves fast, settles into the slot
            float sizeE = t * t;                                // ease-in: holds scene size, shrinks late
            float size = Mathf.Lerp(f.startSize, EndSize, sizeE);
            Place(f.icon, Vector2.Lerp(f.from, TargetFor(f.def), posE), size);
            if (t >= 1f)
            {
                ReturnFlyIcon(f.icon);
                _active.RemoveAt(i);
                Land(f.def, f.amount);
            }
        }
        if (_active.Count == 0) _flyTicker.Pause();
    }

    void WarmFlyPool(int count)
    {
        if (_flyLayer == null) return;
        for (int i = 0; i < count; i++)
        {
            var icon = new Image();            // no sprite -> paints nothing even if it renders before layout
            icon.AddToClassList("fly-icon");   // parked off-screen by USS until rented
            _flyLayer.Add(icon);
            _flyPool.Push(icon);
        }
    }

    Image RentFlyIcon()
    {
        if (_flyPool.Count > 0) return _flyPool.Pop();
        var icon = new Image();
        icon.AddToClassList("fly-icon");
        _flyLayer.Add(icon);
        return icon;
    }

    void ReturnFlyIcon(Image icon)
    {
        icon.sprite = null;
        Place(icon, new Vector2(-9999f, -9999f), 1f);
        _flyPool.Push(icon);
    }

    void Land(ResourceDef def, int amount)
    {
        _displayed[def] = (_displayed.TryGetValue(def, out var n) ? n : 0) + amount;
        Refresh();
        Bounce();
    }

    Vector2 TargetFor(ResourceDef def)
    {
        if (_expanded && _rowIcon.TryGetValue(def, out var icon) && icon.panel != null)
            return icon.worldBound.center;
        return _bag != null ? _bag.worldBound.center : Vector2.zero;
    }

    static void Place(VisualElement icon, Vector2 center, float size)
    {
        icon.style.width = size;
        icon.style.height = size;
        icon.style.left = center.x - size * 0.5f;
        icon.style.top = center.y - size * 0.5f;
    }

    void Bounce()
    {
        if (_bag == null) return;
        _bag.style.scale = new StyleScale(new Scale(new Vector3(1.25f, 1.25f, 1f)));
        _bag.schedule.Execute(() => _bag.style.scale = new StyleScale(new Scale(Vector3.one))).StartingIn(90);
    }

    void Refresh()
    {
        int total = 0;
        foreach (var v in _displayed.Values) total += v;
        if (_capacity != null) _capacity.text = $"{total}/{(_inv?.Capacity ?? 0)}";

        if (_list == null) return;
        _list.Clear();
        _rowIcon.Clear();
        foreach (var kv in _displayed)
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
            _rowIcon[kv.Key] = icon;
        }
    }
}
