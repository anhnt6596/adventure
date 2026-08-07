using System.Collections.Generic;
using Core.UI;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

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

    Damageable _health;
    VisualElement _healthRoot, _healthFill;
    Label _healthText;

    Hunger _hunger;
    VisualElement _hungerRoot, _hungerFill, _hungerMark, _hungerIcon;
    Label _hungerText;

    // Raised by pressing the portrait. Whoever can reach GameScope subscribes and does the opening — the HUD
    // owns the way in, not what is behind it.
    public event System.Action CharacterPanelRequested;

    // The (!) on the portrait: something in the character window is waiting and has not been looked at. Stands
    // for EVERY tab at once, because one face cannot say which — that is what the badges on the tabs are for.
    NotificationService _notifications;
    VisualElement _avatarBadge;

    // Art is App-scope, so unlike everything else here it can simply be injected rather than pushed in.
    IArtProvider _art;

    [Inject]
    public void Construct(IArtProvider art) => _art = art;

    // Level belongs to the CHARACTER, not to the body, so it is pushed as an id + the system that knows
    // about ids — unlike HP and fullness, which are components on whatever body is currently standing there.
    CharacterLevels _levels;
    string _levelCharacterId;
    VisualElement _levelRoot, _levelAvatar, _levelFill;
    Label _levelText, _levelExp;

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
        public PickupSlot slot;
        public ResourceDef def;   // Bag only
        public int amount;        // Bag only
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
        _healthRoot = root.Q<VisualElement>("health");
        _healthFill = root.Q<VisualElement>("health-fill");
        _healthText = root.Q<Label>("health-text");
        _hungerRoot = root.Q<VisualElement>("hunger");
        _hungerFill = root.Q<VisualElement>("hunger-fill");
        _hungerMark = root.Q<VisualElement>("hunger-mark");
        _hungerIcon = root.Q<VisualElement>("hunger-icon");
        _hungerText = root.Q<Label>("hunger-text");
        // The portrait column, not the whole panel: with no character there is no level to show, but the
        // vitals beside it are a separate question and must not vanish with it.
        _levelRoot = root.Q<VisualElement>("avatar-block");
        _levelAvatar = root.Q<VisualElement>("level-avatar");
        _avatarBadge = root.Q<VisualElement>("avatar-badge");
        _levelFill = root.Q<VisualElement>("level-fill");
        _levelText = root.Q<Label>("level-text");
        _levelExp = root.Q<Label>("level-exp");
        root.Q<Button>("capacity-button")?.RegisterCallback<ClickEvent>(_ => Toggle());

        // The portrait IS the button now. The HUD owns the way in but not what it opens: the character window
        // needs GameScope services, and nothing built by the App-scope UISystem can reach those. So it asks,
        // and GameUI answers.
        //
        // On the block rather than on the face, so the level bar under the portrait opens it too — the whole
        // cluster is one thing that answers "who am I", and a player aiming at their level has hit it.
        _levelRoot?.RegisterCallback<ClickEvent>(_ => CharacterPanelRequested?.Invoke());
        _abilityBar = root.Q<VisualElement>("ability-bar");
        WarmFlyPool(20);
    }

    // ---- ability bar ------------------------------------------------------------------------------

    // One button. The fraction is a delegate rather than a stored number because the two things behind these
    // buttons count time differently — an attack winds a timer down in its Update, a skill compares against
    // Time.time — and a button that copied either would be a third place that has to agree.
    class Ability
    {
        public VisualElement Root, Icon, Sweep;
        public System.Func<float> Cooling;   // 1 just spent, 0 ready
        public float Shown = -1f;            // last painted, so a button standing still repaints nothing
    }

    VisualElement _abilityBar;
    readonly List<Ability> _abilities = new();
    IVisualElementScheduledItem _abilityTicker;

    // Rebuilt per body rather than kept: a character switch brings different skills, and three buttons is
    // cheaper to make again than to reconcile.
    public void SetAbilities(MCController mc)
    {
        if (_abilityBar == null) return;

        _abilityBar.Clear();
        _abilities.Clear();

        if (mc == null)
        {
            _abilityTicker?.Pause();
            return;
        }

        // Attack first, and the bar runs left to right, so it sits leftmost — under J, with the skills after
        // it under K and L. It has no component of its own; the unit IS the ability, and "attack" is the key
        // its picture hangs on.
        AddAbility(mc.Id, "attack", () => mc.AttackCooldownFraction);

        var skills = mc.GetComponentsInChildren<CharacterSkill>(true);
        foreach (var slot in new[] { CharacterSkill.Slot.One, CharacterSkill.Slot.Two })
            foreach (var skill in skills)
                if (skill.Which == slot)
                {
                    AddAbility(mc.Id, skill.Key, () => skill.CooldownFraction);
                    break;   // a second skill claiming the same slot has no button, and MCInput has said so
                }

        // 30 a second, not every frame: a wedge is a shape the eye reads as continuous long before it is, and
        // this repaints a mesh each time it moves.
        _abilityTicker ??= Root.schedule.Execute(StepAbilities).Every(33);
        _abilityTicker.Resume();
        StepAbilities();
    }

    void AddAbility(string characterId, string key, System.Func<float> cooling)
    {
        var button = new VisualElement();
        button.AddToClassList("ability");
        button.pickingMode = PickingMode.Ignore;   // a readout, not a control — pressing it does nothing

        var icon = new VisualElement();
        icon.AddToClassList("ability-icon");
        icon.pickingMode = PickingMode.Ignore;
        var sprite = _art?.SkillIcon(characterId, key);
        if (sprite != null) icon.style.backgroundImage = new StyleBackground(sprite);

        var sweep = new VisualElement();
        sweep.AddToClassList("ability-sweep");
        sweep.pickingMode = PickingMode.Ignore;

        var ability = new Ability { Root = button, Icon = icon, Sweep = sweep, Cooling = cooling };
        sweep.generateVisualContent += ctx => PaintSweep(ctx, ability.Shown);

        button.Add(icon);
        button.Add(sweep);
        _abilityBar.Add(button);
        _abilities.Add(ability);
    }

    // Polled rather than pushed, and it does NOT stop when everything is ready. Stopping would need something
    // to start it again on the next press, and neither an attack nor a skill announces being spent — so the
    // saving would be three float reads a tick, bought with a bar that goes dead after the first cooldown.
    // The repaint is the expensive part, and that is what the guard below actually protects.
    void StepAbilities()
    {
        foreach (var ability in _abilities)
        {
            float cooling = Mathf.Clamp01(ability.Cooling?.Invoke() ?? 0f);

            // A whole degree of the dial is the smallest change worth a repaint; below that a mesh is rebuilt
            // for something nobody can see.
            if (Mathf.Abs(cooling - ability.Shown) < 1f / 360f) continue;

            bool wasReady = ability.Shown <= 0f;
            ability.Shown = cooling;
            ability.Sweep.MarkDirtyRepaint();

            if (wasReady == (cooling <= 0f)) continue;   // the ready/cooling state did not turn over
            ability.Root.EnableInClassList("ability--cooling", cooling > 0f);
        }
    }

    // A pie wedge over the icon, shrinking anticlockwise as the wait runs out — the fan opening back up.
    // Painted rather than styled because UI Toolkit has no radial fill: a rectangle cannot be made into an
    // arc by any amount of width, and a rotating mask needs an element per degree of precision.
    //
    // The radius overshoots the button on purpose. The wedge has to reach the CORNERS of a square element to
    // cover it, and .ability clips to its own border radius, which is what trims the overshoot back to a
    // circle for free.
    static void PaintSweep(MeshGenerationContext ctx, float fraction)
    {
        if (fraction <= 0f) return;

        var rect = ctx.visualElement.contentRect;
        if (rect.width <= 0f || rect.height <= 0f) return;

        var centre = rect.center;
        float radius = new Vector2(rect.width, rect.height).magnitude * 0.5f;

        var painter = ctx.painter2D;
        painter.fillColor = new Color(0.02f, 0.03f, 0.05f, 0.78f);
        painter.BeginPath();
        painter.MoveTo(centre);
        // From twelve o'clock, clockwise: the direction a clock hand sweeps is the one people read as time.
        painter.Arc(centre, radius,
                    new Angle(-90f, AngleUnit.Degree),
                    new Angle(-90f + 360f * fraction, AngleUnit.Degree));
        painter.ClosePath();
        painter.Fill();
    }

    public void SetInventory(Inventory inv)
    {
        Unsub();
        _inv = inv;
        SnapToActual();   // pre-existing items appear without a fly
        Sub();
        Refresh();
    }

    // The stomach, bound exactly like the HP bar because it is the same kind of thing: a vital with a
    // ceiling. Nothing here can be opened or managed - food is eaten where it is found.
    public void SetHunger(Hunger hunger)
    {
        if (_hunger != null) _hunger.Changed -= RefreshHunger;
        _hunger = hunger;
        if (_hunger != null) _hunger.Changed += RefreshHunger;
        RefreshHunger();
    }

    void RefreshHunger()
    {
        float max = _hunger != null ? _hunger.Max : 0f;
        float value = _hunger != null ? Mathf.Max(0f, _hunger.Value) : 0f;

        if (_hungerRoot != null) _hungerRoot.style.display = max > 0f ? DisplayStyle.Flex : DisplayStyle.None;
        if (_hungerFill != null) _hungerFill.style.width = Length.Percent(max > 0f ? Mathf.Clamp01(value / max) * 100f : 0f);
        if (_hungerText != null) _hungerText.text = $"{Mathf.CeilToInt(value)}/{Mathf.CeilToInt(max)}";

        // The well-fed line comes from the config, so a retune moves the mark with it rather than leaving the
        // bar quietly lying about where healing starts.
        if (_hungerMark != null && _hunger != null)
            _hungerMark.style.left = Length.Percent(Mathf.Clamp01(_hunger.WellFedFraction) * 100f);
    }

    // The level bar. Takes the system rather than a snapshot so the bar keeps up on its own while the popup
    // or a cheat moves it. The portrait is looked up from the same id, by convention — nothing has to hand
    // one over, and a character that has no art drawn yet simply shows an empty ring.
    public void SetLevels(CharacterLevels levels, string characterId)
    {
        if (_levels != null) _levels.Changed -= OnLevelChanged;
        _levels = levels;
        _levelCharacterId = characterId;
        if (_levels != null) _levels.Changed += OnLevelChanged;

        var avatar = _art?.Avatar(characterId);
        if (_levelAvatar != null)
            _levelAvatar.style.backgroundImage = avatar != null
                ? new StyleBackground(avatar)
                : new StyleBackground(StyleKeyword.None);

        RefreshLevel();
    }

    void OnLevelChanged(string characterId)
    {
        if (characterId == _levelCharacterId) RefreshLevel();
    }

    // Pushed like everything else here that knows about the running game. Taking the service rather than a bool
    // so the badge keeps itself up to date — it has to go out the moment the player opens the window, which is
    // not a moment the HUD would otherwise hear about.
    public void SetNotifications(NotificationService notifications)
    {
        if (_notifications != null) _notifications.Changed -= RefreshBadge;
        _notifications = notifications;
        if (_notifications != null) _notifications.Changed += RefreshBadge;
        RefreshBadge();
    }

    void RefreshBadge()
    {
        if (_avatarBadge == null) return;
        bool show = _notifications != null && _notifications.Any;
        _avatarBadge.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
    }

    void RefreshLevel()
    {
        bool has = _levels != null && !string.IsNullOrEmpty(_levelCharacterId);
        if (_levelRoot != null) _levelRoot.style.display = has ? DisplayStyle.Flex : DisplayStyle.None;
        if (!has) return;

        int level = _levels.Level(_levelCharacterId);
        int exp = _levels.Exp(_levelCharacterId);
        int need = _levels.ExpToNext(_levelCharacterId);

        if (_levelText != null) _levelText.text = level.ToString();   // a number on a face needs no "Lv"

        // At the ceiling the count stops being a count — there is no next level to be part of the way
        // towards, and "x/0" reads as a bug. The bar beside it is full, which says the same thing again.
        if (_levelExp != null)
            _levelExp.text = _levels.IsMaxLevel(_levelCharacterId) ? "MAX" : $"{exp}/{need}";
        if (_levelFill != null) _levelFill.style.width = Length.Percent(_levels.Fraction(_levelCharacterId) * 100f);
    }

    // The player's HP bar. Bound the same way as the inventory (pushed from GameScope), refreshed off the
    // Damageable's HealthChanged. Rebinds cleanly on a respawn/character-switch (a fresh Damageable).
    public void SetHealth(Damageable health)
    {
        if (_health != null) _health.HealthChanged -= RefreshHealth;
        _health = health;
        if (_health != null) _health.HealthChanged += RefreshHealth;
        RefreshHealth();
    }

    void RefreshHealth()
    {
        float max = _health != null ? _health.MaxHp : 0f;
        float hp = _health != null ? Mathf.Max(0f, _health.Hp) : 0f;
        if (_healthRoot != null) _healthRoot.style.display = max > 0f ? DisplayStyle.Flex : DisplayStyle.None;
        if (_healthFill != null) _healthFill.style.width = Length.Percent(max > 0f ? Mathf.Clamp01(hp / max) * 100f : 0f);
        if (_healthText != null) _healthText.text = $"{Mathf.CeilToInt(hp)}/{Mathf.CeilToInt(max)}";
    }

    public override void OnShow() { Sub(); Refresh(); RefreshHealth(); RefreshHunger(); RefreshLevel(); RefreshBadge(); }
    public override void OnHide() { Unsub(); }

    void Sub()
    {
        if (_inv != null) { _inv.Changed -= Reconcile; _inv.Changed += Reconcile; }
        if (_hunger != null) { _hunger.Changed -= RefreshHunger; _hunger.Changed += RefreshHunger; }
        if (_health != null) { _health.HealthChanged -= RefreshHealth; _health.HealthChanged += RefreshHealth; }
        if (_levels != null) { _levels.Changed -= OnLevelChanged; _levels.Changed += OnLevelChanged; }
        if (_notifications != null) { _notifications.Changed -= RefreshBadge; _notifications.Changed += RefreshBadge; }
        PickupFly.Requested -= OnPickup; PickupFly.Requested += OnPickup;
    }

    void Unsub()
    {
        if (_inv != null) _inv.Changed -= Reconcile;
        if (_hunger != null) _hunger.Changed -= RefreshHunger;
        if (_health != null) _health.HealthChanged -= RefreshHealth;
        if (_levels != null) _levels.Changed -= OnLevelChanged;
        if (_notifications != null) _notifications.Changed -= RefreshBadge;
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

    void OnPickup(PickupFlyRequest req)
    {
        var cam = Camera.main;
        if (_flyLayer == null || Root.panel == null || cam == null) { Land(req); return; }

        Vector3 worldPos = req.worldPos;
        float worldHeight = req.worldHeight;

        // Measure along the camera's up (screen-vertical), not world up: a tilted camera foreshortens world
        // Y, which under-read the piece's real on-screen height.
        Vector2 from = RuntimePanelUtils.CameraTransformWorldToPanel(Root.panel, worldPos, cam);
        Vector2 fromTop = RuntimePanelUtils.CameraTransformWorldToPanel(Root.panel, worldPos + cam.transform.up * worldHeight, cam);
        float startSize = Mathf.Clamp(Mathf.Abs(from.y - fromTop.y), EndSize, 1024f);   // on-screen size of the piece

        var icon = RentFlyIcon();
        icon.sprite = req.icon;
        Place(icon, from, startSize);

        _active.Add(new Fly
        {
            icon = icon, slot = req.slot, def = req.def, amount = req.amount,
            from = from, startSize = startSize,
        });
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
            Place(f.icon, Vector2.Lerp(f.from, TargetFor(f), posE), size);
            if (t >= 1f)
            {
                ReturnFlyIcon(f.icon);
                _active.RemoveAt(i);
                Land(f);
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

    // The icon arrived. A resource ticks its shown count up here - the store was credited when it was
    // picked up, so this only ever catches the display up. Food has nothing to tick: the stomach filled at
    // pickup and its bar is bound straight to it, so all that is left is the bounce.
    void Land(Fly f) => Land(f.slot, f.def, f.amount);

    void Land(PickupFlyRequest req) => Land(req.slot, req.def, req.amount);

    void Land(PickupSlot slot, ResourceDef def, int amount)
    {
        if (slot == PickupSlot.Bag && def != null)
        {
            _displayed[def] = (_displayed.TryGetValue(def, out var n) ? n : 0) + amount;
            Refresh();
        }
        Bounce(slot);
    }

    Vector2 TargetFor(Fly f)
    {
        if (f.slot == PickupSlot.Stomach)
            return _hungerIcon != null ? _hungerIcon.worldBound.center : Vector2.zero;

        if (_expanded && f.def != null && _rowIcon.TryGetValue(f.def, out var icon) && icon.panel != null)
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

    void Bounce(PickupSlot slot)
    {
        var target = slot == PickupSlot.Stomach ? _hungerIcon : _bag;
        if (target == null) return;
        target.style.scale = new StyleScale(new Scale(new Vector3(1.25f, 1.25f, 1f)));
        target.schedule.Execute(() => target.style.scale = new StyleScale(new Scale(Vector3.one))).StartingIn(90);
    }

    void Refresh()
    {
        int total = 0;
        foreach (var v in _displayed.Values) total += v;
        // A bare count, not `carried/cap`: the backpack has no cap any more, and the difference in form is
        // what tells the player the supply chip above is a different kind of thing rather than an odd rule.
        if (_capacity != null) _capacity.text = total.ToString();

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
