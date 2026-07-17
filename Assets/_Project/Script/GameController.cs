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

        // single owner of Esc: toggle (PausePopup.CloseOnEscape is false so UISystem won't also close it)
        var pause = _ui.Get<PausePopup>();
        if (pause != null) pause.Close();
        else _ui.Show<PausePopup>();
    }
}
