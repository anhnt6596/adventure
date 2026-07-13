using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;

namespace Core.UI
{
    public enum EffectType
    {
        None,
        Fade,
        Zoom,
    }

    public abstract class PopupAppear
    {
        public abstract UniTask DOShow(CancellationToken token);
        public virtual void Kill() { }
    }

    public abstract class PopupDisappear
    {
        public abstract UniTask DOHide(CancellationToken token);
        public virtual void Kill() { }
    }

    public class PopupAppear_None : PopupAppear
    {
        public override UniTask DOShow(CancellationToken token) => UniTask.CompletedTask;
    }

    public class PopupDisappear_None : PopupDisappear
    {
        public override UniTask DOHide(CancellationToken token) => UniTask.CompletedTask;
    }

    public class PopupAppear_Fade : PopupAppear
    {
        private readonly VisualElement _dimmer;
        private readonly VisualElement _popup;
        private ValueAnimation<float> _dimmerAnim;
        private ValueAnimation<float> _popupAnim;

        public PopupAppear_Fade(VisualElement root)
        {
            _dimmer = root.Q<VisualElement>("dimmer");
            _popup = root.Q<VisualElement>("popup");
        }

        public override UniTask DOShow(CancellationToken token)
        {
            Kill();
            var tcs = new UniTaskCompletionSource();

            token.Register(() =>
            {
                Kill();
                tcs.TrySetCanceled(token);
            });

            _dimmer.style.opacity = 0;
            _popup.style.opacity = 0;
            _popup.style.scale = new Scale(Vector3.one);

            _dimmerAnim = _dimmer.experimental.animation
                .Start(0f, 1f, 300, (e, v) => e.style.opacity = v)
                .Ease(Easing.OutCubic)
                .KeepAlive()
                .OnCompleted(() => _dimmerAnim = null);

            _popupAnim = _popup.experimental.animation
                .Start(0f, 1f, 300, (e, v) => e.style.opacity = v)
                .Ease(Easing.OutCubic)
                .KeepAlive()
                .OnCompleted(() => { _popupAnim = null; tcs.TrySetResult(); });

            return tcs.Task;
        }

        public override void Kill()
        {
            _dimmerAnim?.Stop();
            _dimmerAnim = null;
            _popupAnim?.Stop();
            _popupAnim = null;
        }
    }

    public class PopupDisappear_Fade : PopupDisappear
    {
        private readonly VisualElement _dimmer;
        private readonly VisualElement _popup;
        private ValueAnimation<float> _dimmerAnim;
        private ValueAnimation<float> _popupAnim;

        public PopupDisappear_Fade(VisualElement root)
        {
            _dimmer = root.Q<VisualElement>("dimmer");
            _popup = root.Q<VisualElement>("popup");
        }

        public override UniTask DOHide(CancellationToken token)
        {
            Kill();
            var tcs = new UniTaskCompletionSource();

            token.Register(() =>
            {
                Kill();
                tcs.TrySetCanceled(token);
            });

            _dimmerAnim = _dimmer.experimental.animation
                .Start(1f, 0f, 200, (e, v) => e.style.opacity = v)
                .Ease(Easing.OutSine)
                .KeepAlive()
                .OnCompleted(() => _dimmerAnim = null);

            _popupAnim = _popup.experimental.animation
                .Start(1f, 0f, 200, (e, v) => e.style.opacity = v)
                .Ease(Easing.OutSine)
                .KeepAlive()
                .OnCompleted(() => { _popupAnim = null; tcs.TrySetResult(); });

            return tcs.Task;
        }

        public override void Kill()
        {
            _dimmerAnim?.Stop();
            _dimmerAnim = null;
            _popupAnim?.Stop();
            _popupAnim = null;
        }
    }

    public class PopupAppear_Zoom : PopupAppear
    {
        private readonly VisualElement _dimmer;
        private readonly VisualElement _popup;
        private ValueAnimation<float> _dimmerAnim;
        private ValueAnimation<float> _popupAnim;

        public PopupAppear_Zoom(VisualElement root)
        {
            _dimmer = root.Q<VisualElement>("dimmer");
            _popup = root.Q<VisualElement>("popup");
        }

        public override UniTask DOShow(CancellationToken token)
        {
            Kill();
            var tcs = new UniTaskCompletionSource();

            token.Register(() =>
            {
                Kill();
                tcs.TrySetCanceled(token);
            });

            _dimmer.style.opacity = 0;
            _popup.style.opacity = 1;
            _popup.style.scale = new Scale(Vector3.zero);

            _dimmerAnim = _dimmer.experimental.animation
                .Start(0f, 1f, 300, (e, v) => e.style.opacity = v)
                .Ease(Easing.OutCubic)
                .KeepAlive()
                .OnCompleted(() => _dimmerAnim = null);

            _popupAnim = _popup.experimental.animation
                .Start(0f, 1f, 300, (e, v) => e.style.scale = new Scale(new Vector3(v, v, 1)))
                .Ease(Easing.OutBack)
                .KeepAlive()
                .OnCompleted(() => { _popupAnim = null; tcs.TrySetResult(); });

            return tcs.Task;
        }

        public override void Kill()
        {
            _dimmerAnim?.Stop();
            _dimmerAnim = null;
            _popupAnim?.Stop();
            _popupAnim = null;
        }
    }

    public class PopupDisappear_Zoom : PopupDisappear
    {
        private readonly VisualElement _dimmer;
        private readonly VisualElement _popup;
        private ValueAnimation<float> _dimmerAnim;
        private ValueAnimation<float> _popupAnim;

        public PopupDisappear_Zoom(VisualElement root)
        {
            _dimmer = root.Q<VisualElement>("dimmer");
            _popup = root.Q<VisualElement>("popup");
        }

        public override UniTask DOHide(CancellationToken token)
        {
            Kill();
            var tcs = new UniTaskCompletionSource();

            token.Register(() =>
            {
                Kill();
                tcs.TrySetCanceled(token);
            });

            _dimmerAnim = _dimmer.experimental.animation
                .Start(1f, 0f, 200, (e, v) => e.style.opacity = v)
                .Ease(Easing.InCubic)
                .KeepAlive()
                .OnCompleted(() => _dimmerAnim = null);

            _popupAnim = _popup.experimental.animation
                .Start(1f, 0f, 200, (e, v) => e.style.scale = new Scale(new Vector3(v, v, 1)))
                .Ease(Easing.InBack)
                .KeepAlive()
                .OnCompleted(() => { _popupAnim = null; tcs.TrySetResult(); });

            return tcs.Task;
        }

        public override void Kill()
        {
            _dimmerAnim?.Stop();
            _dimmerAnim = null;
            _popupAnim?.Stop();
            _popupAnim = null;
        }
    }
}
