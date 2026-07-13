using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;

namespace Core.UI
{
    public abstract class BaseSheet : UIView, ISheet
    {
        protected virtual ScreenTapDismiss DismissOnScreenTap => ScreenTapDismiss.DismissOnTapBlank;

        private CancellationTokenSource _cts;
        private ValueAnimation<float> _anim;
        private bool _isClosing;

        protected VisualElement _sheet;

        public BaseSheet(VisualElement root) : base(root)
        {
            root.pickingMode = PickingMode.Ignore;
            _sheet = root.Q<VisualElement>("sheet");
        }

        public override void OnShow()
        {
            _isClosing = false;
            base.OnShow();

            if (DismissOnScreenTap != ScreenTapDismiss.None)
            {
                _uiSystem.OnScreenTapped += OnScreenTapped;
            }
        }

        private void OnScreenTapped(Vector2 panelPos)
        {
            var localPos = _sheet.WorldToLocal(panelPos);
            if (_sheet.ContainsPoint(localPos)) return;

            if (DismissOnScreenTap == ScreenTapDismiss.DismissOnTapBlank)
            {
                var picked = Root.panel.Pick(panelPos);
                if (picked != null && picked.pickingMode == PickingMode.Position) return;
            }

            Close();
        }

        public void Close()
        {
            if (_isClosing) return;
            CloseTask().Forget();
        }

        public void DoShowFx() => DoShowFxTask().Forget();

        private UniTask DoShowFxTask()
        {
            KillAnim();
            var tcs = new UniTaskCompletionSource();

            _sheet.style.translate = new Translate(0, Length.Percent(100));

            _anim = _sheet.experimental.animation
                .Start(100f, 0f, 300, (e, v) => e.style.translate = new Translate(0, Length.Percent(v)))
                .Ease(Easing.OutCubic)
                .KeepAlive()
                .OnCompleted(() => { _anim = null; tcs.TrySetResult(); });

            return tcs.Task;
        }

        private async UniTask CloseTask()
        {
            _isClosing = true;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            await DOHide(_cts.Token);
            if (_cts.Token.IsCancellationRequested) return;
            _uiSystem.Hide(this);
        }

        private UniTask DOHide(CancellationToken token)
        {
            KillAnim();
            var tcs = new UniTaskCompletionSource();

            token.Register(() =>
            {
                KillAnim();
                tcs.TrySetCanceled(token);
            });

            _anim = _sheet.experimental.animation
                .Start(0f, 100f, 200, (e, v) => e.style.translate = new Translate(0, Length.Percent(v)))
                .Ease(Easing.InCubic)
                .KeepAlive()
                .OnCompleted(() => { _anim = null; tcs.TrySetResult(); });

            return tcs.Task;
        }

        private void KillAnim()
        {
            _anim?.Stop();
            _anim = null;
        }

        public override void OnHide()
        {
            _isClosing = false;
            if (DismissOnScreenTap != ScreenTapDismiss.None)
            {
                _uiSystem.OnScreenTapped -= OnScreenTapped;
            }
            _cts?.Cancel();
            KillAnim();
            base.OnHide();
        }
    }
}
