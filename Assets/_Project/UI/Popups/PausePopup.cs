using System;
using Core;
using Core.UI;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

// UXML/USS live next to this file and MUST be named PausePopup.uxml (registry matches by file name).
public class PausePopup : BasePopup
{
    // Esc is owned by GameController (toggle), so don't let UISystem close us on Esc too.
    public override bool CloseOnEscape => false;

    IInputGate _gate;
    ISceneService _scenes;

    IDisposable _block;
    float _prevTimeScale = 1f;

    public PausePopup(VisualElement root) : base(root)
    {
        root.Q<Button>("resume-button")?.RegisterCallback<ClickEvent>(_ => Close());
        root.Q<Button>("restart-button")?.RegisterCallback<ClickEvent>(_ => Restart());
    }

    [Inject]
    public void Construct(IInputGate gate, ISceneService scenes)
    {
        _gate = gate;
        _scenes = scenes;
    }

    public override void OnShow()
    {
        base.OnShow();
        _block = _gate?.Block(InputKind.All, "pause");
        _prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;                       // freeze the world (UI Toolkit FX runs unscaled)
    }

    public override void OnHide()
    {
        Time.timeScale = _prevTimeScale;
        _block?.Dispose();
        _block = null;
        base.OnHide();
    }

    void Restart()
    {
        Time.timeScale = _prevTimeScale;           // never leave a loading scene frozen
        Close();
        _scenes?.LoadAsync(LoadingFlow.GameSceneName).Forget();
    }
}
