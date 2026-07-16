using Core.UI;
using UnityEngine.InputSystem;
using VContainer.Unity;

public class GameController : IStartable, ITickable
{
    readonly IUISystem _ui;

    public GameController(IUISystem ui) => _ui = ui;

    public void Start()
    {
    }

    public void Tick()
    {
        var kb = Keyboard.current;
        if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;

        // single owner of Esc: toggle (PausePopup.CloseOnEscape is false so UISystem won't also close it)
        var pause = _ui.Get<PausePopup>();
        if (pause != null) pause.Close();
        else _ui.Show<PausePopup>();
    }
}
