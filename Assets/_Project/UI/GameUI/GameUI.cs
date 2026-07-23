using System;
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
    }

    void OnDestroy() => Release();

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
