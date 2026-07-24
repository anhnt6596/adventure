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
    IGetMCConfig _cheatMcConfig;
    DropdownField _mcDropdown;
    Button _changeMcButton;

    VisualElement _bagList;
    Inventory _bagInventory;        // the live one; re-pointed when the player switches

    // Separate [Inject] method so the cheat panel's dependencies exist only in the editor — the build's
    // Construct signature stays untouched.
    [Inject]
    public void ConstructCheats(PlayerSystem player, IGetMCConfig mcConfig)
    {
        _cheatPlayer = player;
        _cheatMcConfig = mcConfig;
    }
#endif

    [Inject]
    public void Construct(IInputGate gate, IUISystem ui, IPlayer player)
    {
        _gate = gate;
        _ui = ui;
        _player = player;   // the HUD will read the player's inventory (and, later, lots more) off this
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

        SetupCheats(root);
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
        if (_mcDropdown == null || _changeMcButton == null || _cheatMcConfig == null) return;

        _mcDropdown.choices = new List<string>(_cheatMcConfig.Ids);
        // Runtime DropdownField can't disable a single entry, so the live one is marked here and the Change
        // button greys out instead — same effect, no custom dropdown.
        _mcDropdown.formatListItemCallback = id => id == CurrentMcId ? $"{id}  (current)" : id;
        _mcDropdown.RegisterValueChangedCallback(_ => RefreshCharacterCheat());
        _changeMcButton.RegisterCallback<ClickEvent>(_ => ChangeMc());

        if (_player != null) _player.Spawned += OnPlayerSpawned;
        RefreshCharacterCheat();
    }

    string CurrentMcId => _player?.Current != null ? _player.Current.Id : null;

    void OnPlayerSpawned(MCController _)
    {
        RefreshCharacterCheat();
        RebindBag();   // a switched character has its own inventory
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

    void RefreshCharacterCheat()
    {
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
#if UNITY_EDITOR
        if (_player != null) _player.Spawned -= OnPlayerSpawned;
        if (_bagInventory != null) _bagInventory.Changed -= RefreshBag;
#endif
    }

    void StartGame()
    {
        Release();
        if (_screen != null) _screen.style.display = DisplayStyle.None;

        _ui?.Show<GameHUD>();                          // reveal the in-game HUD
        // the player's own inventory — its Picker created it with the current character's config
        var inventory = _player.Current != null ? _player.Current.GetComponentInChildren<Picker>()?.Inventory : null;
        _ui?.Get<GameHUD>()?.SetInventory(inventory);
    }

    void Release()
    {
        _block?.Dispose();
        _block = null;
    }
}
