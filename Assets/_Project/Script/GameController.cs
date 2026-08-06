using Core.UI;
using Cysharp.Threading.Tasks;
using UnityEngine.InputSystem;
using VContainer.Unity;

public class GameController : IStartable, ITickable
{
    const string StartMapId = "Map_1";
    const int StartGateIndex = 0;

    readonly IUISystem _ui;
    readonly IMapService _maps;

    public GameController(IUISystem ui, IMapService maps)
    {
        _ui = ui;
        _maps = maps;
    }

    public void Start() => _maps.WarpAsync(StartMapId, StartGateIndex).Forget();

    public void Tick()
    {
        var kb = Keyboard.current;
        if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;

        // Esc means "back out of what is on top", so a window that is already up gets it first. CharacterPopup
        // closes itself on Esc (CloseOnEscape is true, so UISystem does it), and all this has to do is not open
        // a pause menu over the top of it in the same frame.
        if (_ui.Get<CharacterPopup>() != null) return;

        // single owner of Esc: toggle (PausePopup.CloseOnEscape is false so UISystem won't also close it)
        var pause = _ui.Get<PausePopup>();
        if (pause != null) pause.Close();
        else _ui.Show<PausePopup>();
    }
}
