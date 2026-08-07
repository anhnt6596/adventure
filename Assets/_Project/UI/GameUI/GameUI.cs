using System;
using System.Collections.Generic;
using Core.UI;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

// The start menu: its own UIDocument (not the UISystem registry), holding START and later settings,
// change-character, etc. Blocks gameplay input until START, then reveals the game HUD.
[RequireComponent(typeof(UIDocument))]
public class GameUI : MonoBehaviour
{
    IInputGate _gate;
    IUISystem _ui;
    IPlayer _player;
    IDisposable _block;
    UIDocument _document;
    VisualElement _screen;
#if UNITY_EDITOR
    VisualElement _cheatPanel;
    Button _cheatToggle;
    bool _cheatsOpen;

    PlayerSystem _cheatPlayer;      // IPlayer is read-only; switching character needs the concrete system
    IGetMCConfig _mcConfig;         // only the character dropdown needs it, so it stays out of the build
    DropdownField _mcDropdown;
    Button _changeMcButton;

    // Nothing awards experience yet, so this is the only way to move a level at all — which is the point:
    // the tree cannot be played with until something can pay for it.
    Label _levelReadout;
    IntegerField _expField, _levelField;

    VisualElement _bagList;
    Inventory _bagInventory;        // the live one; re-pointed when the player switches

    VisualElement _statList;
    ICharacterStats _cheatStats;    // the live body's set — read-only, this only shows it

    Slider _hpSlider, _hungerSlider;
    Toggle _noHungerToggle, _invincibleToggle;
    Damageable _cheatHealth;
    Hunger _cheatHunger;

    // Separate [Inject] method so the cheat panel's dependencies exist only in the editor — the build's
    // Construct signature stays untouched.
    [Inject]
    public void ConstructCheats(PlayerSystem player, IGetMCConfig mcConfig)
    {
        _cheatPlayer = player;
        _mcConfig = mcConfig;
    }
#endif

    CharacterLevels _levels;
    IGetUpgradeTree _trees;
    UpgradeSystem _upgrades;
    NotificationService _notifications;

    [Inject]
    public void Construct(IInputGate gate, IUISystem ui, IPlayer player,
                          CharacterLevels levels, IGetUpgradeTree trees, UpgradeSystem upgrades,
                          NotificationService notifications)
    {
        _gate = gate;
        _ui = ui;
        _player = player;   // the HUD will read the player's inventory (and, later, lots more) off this
        _levels = levels;
        _trees = trees;
        _upgrades = upgrades;
        _notifications = notifications;
    }

    void Awake() => _document = GetComponent<UIDocument>();

    void Start()
    {
        var root = _document.rootVisualElement;
        _screen = root.Q<VisualElement>("screen");

        // hold input until the player presses START (Start() runs before any Update)
        if (_gate == null)
            Debug.LogError($"[{nameof(GameUI)}] IInputGate not injected — add this GameObject to GameScope's Auto Inject Game Objects, or input won't be blocked.", this);
        _block = _gate?.Block(InputKind.All, "pre-start");

        root.Q<Button>("start-button")?.RegisterCallback<ClickEvent>(_ => StartGame());

        // Each body is a fresh Inventory + Damageable, so a respawn or a character switch leaves the HUD
        // pointed at the dead one — re-bind on every spawn, not just at START.
        if (_player != null) _player.Spawned += OnPlayerSpawned;

        // Buying a node can add a button to the bar, and the popup is open when it happens — waiting for the
        // next spawn to show it would mean unlocking a skill and seeing nothing change. Nothing else on the
        // HUD is rebuilt by this, so it is the bar and only the bar.
        if (_upgrades != null) _upgrades.Changed += RefreshAbilities;

        SetupCheats(root);
    }

    // PlayerSystem is subscribed to the same event and is what actually opens the skill, so ordering matters:
    // it registers in its constructor, at container build, long before this runs in Start. The bar therefore
    // reads a skill that has already been told.
    void RefreshAbilities()
    {
        _ui?.Get<GameHUD>()?.SetAbilities(_player?.Current);
    }

    void OnPlayerSpawned(MCController _)
    {
        BindHud();
#if UNITY_EDITOR
        RefreshCharacterCheat();
        RebindBag();        // a switched character has its own inventory
        RebindVitals();     // ...and its own body, so its own HP and stomach
        RebindStats();      // ...and a set of stats built fresh for that body
        RefreshLevelCheat();// ...and its own level
#endif
    }

    // Show first, then feed it: Show runs the popup's OnShow, and the popup cannot resolve any of this for
    // itself (UISystem builds views with the App container). Same push the HUD gets, for the same reason.
    void OpenCharacterPanel()
    {
        var popup = _ui?.Show<CharacterPopup>();
        if (popup == null) return;

        var id = _player?.Current != null ? _player.Current.Id : null;
        popup.Bind(_trees, _upgrades, _levels, _notifications, id);
    }

    // Point the HUD at the live body. Before START the HUD isn't shown and Get returns null, so this is a
    // no-op then — StartGame binds it the moment it reveals it.
    void BindHud()
    {
        var hud = _ui?.Get<GameHUD>();
        if (hud == null) return;

        // -= first: BindHud runs on every spawn, and the HUD instance is pooled across all of them.
        hud.CharacterPanelRequested -= OpenCharacterPanel;
        hud.CharacterPanelRequested += OpenCharacterPanel;

        // Not per-body — the service is one for the whole game — but bound here anyway, because this is the
        // method that runs once the HUD exists at all. SetNotifications re-subscribes cleanly.
        hud.SetNotifications(_notifications);

        var mc = _player?.Current;
        var picker = mc != null ? mc.GetComponentInChildren<Picker>() : null;
        hud.SetAbilities(mc);   // attack + whichever skills this body carries
        hud.SetInventory(picker?.Inventory);
        hud.SetHealth(mc != null ? mc.GetComponentInChildren<Damageable>() : null);
        hud.SetHunger(mc != null ? mc.GetComponentInChildren<Hunger>() : null);

        // Level is the character's, not the body's, so this one is bound by id — and the id is all it needs:
        // the HUD finds the portrait itself, by name.
        hud.SetLevels(_levels, mc != null ? mc.Id : null);
    }

    // Dev drawer on the left edge: the tab opens a vertical panel that will hold the cheat tools (empty for
    // now — they go under "cheat-tools"). Editor only, and physically removed elsewhere so it can't ship.
    void SetupCheats(VisualElement root)
    {
        var cheat = root.Q<VisualElement>("cheat");
        if (cheat == null) return;
#if UNITY_EDITOR
        _cheatPanel = cheat.Q<VisualElement>("cheat-panel");
        _cheatToggle = cheat.Q<Button>("cheat-toggle");
        _cheatToggle?.RegisterCallback<ClickEvent>(_ => ToggleCheats());
        SetupCharacterCheat(cheat);
        SetupStatCheat(cheat);
        SetupVitalCheat(cheat);
        SetupLevelCheat(cheat);
        SetupBagCheat(cheat);
#else
        cheat.RemoveFromHierarchy();
#endif
    }

#if UNITY_EDITOR
    // Tracked in a field rather than read back off style.display: an inline display that was never set
    // reports as undefined, not as the value the USS resolved to.
    void ToggleCheats()
    {
        _cheatsOpen = !_cheatsOpen;
        if (_cheatPanel != null) _cheatPanel.style.display = _cheatsOpen ? DisplayStyle.Flex : DisplayStyle.None;
        if (_cheatToggle != null) _cheatToggle.text = _cheatsOpen ? "«" : "»";
    }

    // Swap the live MC. Ids come from IGetMCConfig (the same wall PlayerSystem uses), so the list can never
    // drift from what can actually be spawned.
    void SetupCharacterCheat(VisualElement cheat)
    {
        _mcDropdown = cheat.Q<DropdownField>("mc-dropdown");
        _changeMcButton = cheat.Q<Button>("change-mc-button");
        if (_mcDropdown == null || _changeMcButton == null || _mcConfig == null) return;

        _mcDropdown.choices = new List<string>(_mcConfig.Ids);
        // Runtime DropdownField can't disable a single entry, so the live one is marked here and the Change
        // button greys out instead — same effect, no custom dropdown.
        _mcDropdown.formatListItemCallback = id => id == CurrentMcId ? $"{id}  (current)" : id;
        _mcDropdown.RegisterValueChangedCallback(_ => RefreshCharacterCheat());
        _changeMcButton.RegisterCallback<ClickEvent>(_ => ChangeMc());

        RefreshCharacterCheat();
    }

    string CurrentMcId => _player?.Current != null ? _player.Current.Id : null;

    // Every stat the character has, as it stands right now. Read-only on purpose: this is the panel that
    // answers "did that upgrade actually land, and how much of it" — the numbers are moved from the tree, or
    // from the vitals below, and this is where you watch what they did.
    //
    // It shows the FINAL value and not the base, because that is the only thing ICharacterStats offers and
    // rightly so: a base is the character's own number and belongs to whoever may set it.
    void SetupStatCheat(VisualElement cheat)
    {
        _statList = cheat.Q<VisualElement>("stat-list");
        if (_statList != null) RebindStats();   // the player may already be spawned
    }

    void RebindStats()
    {
        if (_statList == null) return;

        // Stats are built per spawn, so the old set outlives nothing — but it can still fire while a timed
        // buff on it expires, and a listener left on it would redraw the panel from a body that is gone.
        var stats = _player?.Current != null ? _player.Current.Stats : null;
        if (stats != _cheatStats)
        {
            ListenToStats(_cheatStats, false);
            _cheatStats = stats;
            ListenToStats(_cheatStats, true);
        }
        RefreshStats();
    }

    void ListenToStats(ICharacterStats stats, bool on)
    {
        if (stats == null) return;

        foreach (var id in StatId.All)
        {
            var stat = stats.Get(id);
            if (stat == null) continue;
            if (on) stat.Changed += RefreshStats;
            else stat.Changed -= RefreshStats;
        }
    }

    // Rebuilt whole rather than each row updated in place: nine labels is nothing, and a rebuild cannot
    // fall out of step with a stat list that grows.
    void RefreshStats()
    {
        if (_statList == null) return;
        _statList.Clear();

        if (_cheatStats == null)
        {
            var none = new Label("no character");
            none.AddToClassList("cheat-bag-empty");
            _statList.Add(none);
            return;
        }

        foreach (var id in StatId.All)
        {
            var stat = _cheatStats.Get(id);
            if (stat == null) continue;

            var row = new VisualElement();
            row.AddToClassList("cheat-stat-row");

            var name = new Label(id);
            name.AddToClassList("cheat-stat-name");

            var value = new Label(stat.Value.ToString("0.##"));
            value.AddToClassList("cheat-stat-value");

            row.Add(name);
            row.Add(value);
            _statList.Add(row);
        }
    }

    // Vitals cheat: drag HP or fullness anywhere, or switch off hunger and damage entirely.
    //
    // Every one of these drives a REAL api — Heal/TakeDamage, Eat/Drain, and a StatModifier on the drain —
    // for the same reason the bag's remove button does. A cheat that takes a private back door stops testing
    // the thing it is pointed at, and is the first thing to rot when that thing changes.
    void SetupVitalCheat(VisualElement cheat)
    {
        _hpSlider = cheat.Q<Slider>("hp-slider");
        _hungerSlider = cheat.Q<Slider>("hunger-slider");
        _noHungerToggle = cheat.Q<Toggle>("no-hunger-toggle");
        _invincibleToggle = cheat.Q<Toggle>("invincible-toggle");

        _hpSlider?.RegisterValueChangedCallback(e => ApplyHp(e.newValue));
        _hungerSlider?.RegisterValueChangedCallback(e => ApplyHunger(e.newValue));
        _noHungerToggle?.RegisterValueChangedCallback(e => { if (_cheatHunger != null) _cheatHunger.DrainPaused = e.newValue; });
        _invincibleToggle?.RegisterValueChangedCallback(e => { if (_cheatHealth != null) _cheatHealth.Invulnerable = e.newValue; });

        RebindVitals();   // the player may already be spawned
    }

    void RebindVitals()
    {
        if (_hpSlider == null && _hungerSlider == null) return;

        var mc = _player?.Current;
        var health = mc != null ? mc.GetComponentInChildren<Damageable>() : null;
        var hunger = mc != null ? mc.GetComponentInChildren<Hunger>() : null;

        if (health != _cheatHealth)
        {
            if (_cheatHealth != null) _cheatHealth.HealthChanged -= PushVitals;
            _cheatHealth = health;
            if (_cheatHealth != null) _cheatHealth.HealthChanged += PushVitals;
        }
        if (hunger != _cheatHunger)
        {
            if (_cheatHunger != null) _cheatHunger.Changed -= PushVitals;
            _cheatHunger = hunger;
            if (_cheatHunger != null) _cheatHunger.Changed += PushVitals;
        }

        // A fresh body starts clean, so the toggles show what is actually true rather than what was ticked
        // for the last one.
        if (_noHungerToggle != null && _cheatHunger != null) _cheatHunger.DrainPaused = _noHungerToggle.value;
        if (_invincibleToggle != null && _cheatHealth != null) _cheatHealth.Invulnerable = _invincibleToggle.value;

        PushVitals();
    }

    // Track the live values without re-triggering the callbacks that wrote them.
    void PushVitals()
    {
        if (_hpSlider != null)
        {
            _hpSlider.highValue = _cheatHealth != null ? Mathf.Max(1f, _cheatHealth.MaxHp) : 1f;
            _hpSlider.SetValueWithoutNotify(_cheatHealth != null ? _cheatHealth.Hp : 0f);
            _hpSlider.SetEnabled(_cheatHealth != null);
        }
        if (_hungerSlider != null)
        {
            _hungerSlider.highValue = _cheatHunger != null ? Mathf.Max(1f, _cheatHunger.Max) : 1f;
            _hungerSlider.SetValueWithoutNotify(_cheatHunger != null ? _cheatHunger.Value : 0f);
            _hungerSlider.SetEnabled(_cheatHunger != null);
        }
    }

    // Move by the difference through the real paths, rather than writing HP straight in. Dragging to zero
    // therefore kills exactly the way an attack does — drops, death screen and all — which is most of why
    // you would drag it to zero.
    void ApplyHp(float target)
    {
        if (_cheatHealth == null) return;
        float delta = target - _cheatHealth.Hp;
        if (delta > 0f) _cheatHealth.Heal(delta);
        else if (delta < 0f) _cheatHealth.TakeDamage(-delta, this);
    }

    void ApplyHunger(float target)
    {
        if (_cheatHunger == null) return;
        float delta = target - _cheatHunger.Value;
        if (delta > 0f) _cheatHunger.Eat(delta);
        else if (delta < 0f) _cheatHunger.Drain(-delta);
    }

    // Level cheat. Add exp goes through the REAL AddExp, so it levels up exactly the way a kill eventually
    // will — carrying over the remainder, and stepping through several levels if the number is big enough.
    // Set level is the blunt one, and says so by dropping the part-level progress.
    void SetupLevelCheat(VisualElement cheat)
    {
        _levelReadout = cheat.Q<Label>("level-readout");
        _expField = cheat.Q<IntegerField>("exp-field");
        _levelField = cheat.Q<IntegerField>("level-field");

        cheat.Q<Button>("add-exp-button")?.RegisterCallback<ClickEvent>(_ =>
            _levels?.AddExp(CurrentMcId, Mathf.Max(0, _expField?.value ?? 0)));

        cheat.Q<Button>("set-level-button")?.RegisterCallback<ClickEvent>(_ =>
            _levels?.SetLevel(CurrentMcId, Mathf.Max(CharacterLevels.StartLevel, _levelField?.value ?? 1)));

        // Named, not a lambda: OnDestroy has to be able to take it back off again.
        if (_levels != null) _levels.Changed += OnAnyLevelChanged;
        RefreshLevelCheat();
    }

    void OnAnyLevelChanged(string _) => RefreshLevelCheat();

    void RefreshLevelCheat()
    {
        if (_levelReadout == null) return;

        var id = CurrentMcId;
        if (string.IsNullOrEmpty(id) || _levels == null) { _levelReadout.text = "—"; return; }

        _levelReadout.text = _levels.IsMaxLevel(id)
            ? $"{id}   Lv {_levels.Level(id)}   MAX"
            : $"{id}   Lv {_levels.Level(id)}   {_levels.Exp(id)}/{_levels.ExpToNext(id)}";
    }

    // Bag cheat: list each resource kind held, with a button to wipe that kind. The remove path is a real
    // Inventory.Remove (crafting/spending will use it too), not a cheat-only shortcut.
    void SetupBagCheat(VisualElement cheat)
    {
        _bagList = cheat.Q<VisualElement>("bag-list");
        if (_bagList != null) RebindBag();   // the player may already be spawned
    }

    Inventory CurrentBag => _player?.Current != null ? _player.Current.GetComponentInChildren<Picker>()?.Inventory : null;

    void RebindBag()
    {
        if (_bagList == null) return;

        var inv = CurrentBag;
        if (inv != _bagInventory)
        {
            if (_bagInventory != null) _bagInventory.Changed -= RefreshBag;
            _bagInventory = inv;
            if (_bagInventory != null) _bagInventory.Changed += RefreshBag;
        }
        RefreshBag();
    }

    void RefreshBag()
    {
        if (_bagList == null) return;
        _bagList.Clear();

        bool any = false;
        if (_bagInventory != null)
            foreach (var kv in _bagInventory.Counts)
            {
                if (kv.Value <= 0) continue;
                any = true;

                var def = kv.Key;
                int n = kv.Value;

                var row = new VisualElement();
                row.AddToClassList("cheat-bag-row");

                var name = new Label($"{def.Id}  ×{n}");
                name.AddToClassList("cheat-bag-name");

                var remove = new Button(() => _bagInventory.Remove(def, n)) { text = "×" };   // wipe this kind
                remove.AddToClassList("cheat-bag-remove");

                row.Add(name);
                row.Add(remove);
                _bagList.Add(row);
            }

        if (!any)
        {
            var empty = new Label("empty");
            empty.AddToClassList("cheat-bag-empty");
            _bagList.Add(empty);
        }
    }

    // Also reached from OnPlayerSpawned, which fires whether or not the cheat panel wired up.
    void RefreshCharacterCheat()
    {
        if (_mcDropdown == null || _changeMcButton == null) return;

        var current = CurrentMcId;
        if (_mcDropdown.value == null || !_mcDropdown.choices.Contains(_mcDropdown.value))
            _mcDropdown.SetValueWithoutNotify(current ?? (_mcDropdown.choices.Count > 0 ? _mcDropdown.choices[0] : null));

        _changeMcButton.SetEnabled(!string.IsNullOrEmpty(_mcDropdown.value) && _mcDropdown.value != current);
    }

    void ChangeMc()
    {
        var id = _mcDropdown.value;
        if (string.IsNullOrEmpty(id) || id == CurrentMcId) return;
        _cheatPlayer?.SwitchTo(id);   // logs and keeps the current body if that id can't spawn
    }
#endif

    void OnDestroy()
    {
        Release();
        if (_player != null) _player.Spawned -= OnPlayerSpawned;
        if (_upgrades != null) _upgrades.Changed -= RefreshAbilities;
#if UNITY_EDITOR
        if (_bagInventory != null) _bagInventory.Changed -= RefreshBag;
        if (_levels != null) _levels.Changed -= OnAnyLevelChanged;
        ListenToStats(_cheatStats, false);
#endif
    }

    void StartGame()
    {
        Release();
        if (_screen != null) _screen.style.display = DisplayStyle.None;

        _ui?.Show<GameHUD>();   // reveal the in-game HUD
        BindHud();
    }

    void Release()
    {
        _block?.Dispose();
        _block = null;
    }
}
