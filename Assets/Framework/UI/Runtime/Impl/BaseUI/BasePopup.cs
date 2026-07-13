using System;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine.UIElements;

namespace Core.UI
{
    public abstract class BasePopup : UIView, IPopup
    {
        public override UILayer DefaultLayer { get; } = UILayer.Popup;
        protected virtual EffectType AppearFx { get; } = EffectType.Zoom;
        protected virtual EffectType DisappearFx { get; } = EffectType.Fade;
        protected virtual bool CloseWhenClickBackground { get; } = true;
        public virtual bool CloseOnEscape { get; } = true;

        public event Action AppearCompleted;

        protected PopupAppear _appearSM;
        protected PopupDisappear _disappearSM;

        private CancellationTokenSource _cts;
        private bool _isClosing;

        protected VisualElement _dimmer;
        protected VisualElement _popup;

        public BasePopup(VisualElement root) : base(root)
        {
            _dimmer = root.Q<VisualElement>("dimmer");
            _popup = root.Q<VisualElement>("popup");

            _appearSM = GetFXSMAppear();
            _disappearSM = GetFXSMDisappear();

            if (CloseWhenClickBackground) AddDimmerClicked();
        }

        private PopupAppear GetFXSMAppear()
        {
            switch (AppearFx)
            {
                case EffectType.Fade:
                    return new PopupAppear_Fade(Root);
                case EffectType.Zoom:
                    return new PopupAppear_Zoom(Root);
                default:
                    return new PopupAppear_None();
            }
        }

        private PopupDisappear GetFXSMDisappear()
        {
            switch (DisappearFx)
            {
                case EffectType.Fade:
                    return new PopupDisappear_Fade(Root);
                case EffectType.Zoom:
                    return new PopupDisappear_Zoom(Root);
                default:
                    return new PopupDisappear_None();
            }
        }

        private void AddDimmerClicked()
        {
            _dimmer.RegisterCallback<ClickEvent>(_ => Close());

            _popup.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
            });
        }

        public virtual void Close()
        {
            if (_isClosing) return;
            CloseTask().Forget();
        }

        private async UniTask CloseTask()
        {
            _isClosing = true;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            await _disappearSM.DOHide(_cts.Token);
            if (_cts.Token.IsCancellationRequested) return;
            _uiSystem.Hide(this);
        }

        public override void OnShow()
        {
            _isClosing = false;
            base.OnShow();
            ShowAppearFx().Forget();
        }

        private async UniTaskVoid ShowAppearFx()
        {
            await _appearSM.DOShow();
            if (_isClosing) return;
            AppearCompleted?.Invoke();
        }

        public override void OnHide()
        {
            _isClosing = false;
            _cts?.Cancel();
            _disappearSM.Kill();
            base.OnHide();
        }

    }
}
