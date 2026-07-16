using System;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

// Scene UI for GameScene (its own UIDocument, not part of the UISystem registry).
// Blocks gameplay input until START is pressed.
[RequireComponent(typeof(UIDocument))]
public class GameUI : MonoBehaviour
{
    IInputGate _gate;
    IDisposable _block;
    UIDocument _document;
    VisualElement _screen;

    [Inject]
    public void Construct(IInputGate gate) => _gate = gate;

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
        // or: _document.rootVisualElement.style.display = DisplayStyle.None;  /  gameObject.SetActive(false)
    }

    void Release()
    {
        _block?.Dispose();
        _block = null;
    }
}
