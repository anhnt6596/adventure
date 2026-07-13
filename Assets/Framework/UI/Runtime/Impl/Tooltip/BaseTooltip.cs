using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;
using static Core.UI.VisualElementExtensions;

namespace Core.UI
{
    public abstract class BaseTooltip : UIView, ITooltip
    {
        public override UILayer DefaultLayer { get; } = UILayer.Overlay;

        protected virtual bool DismissOnTapOutside => true;

        private VisualElement _body;
        private VisualElement _arrow;
        private ValueAnimation<float> _anim;
        private IVisualElementScheduledItem _autoHideSchedule;

        public BaseTooltip(VisualElement root) : base(root)
        {
            root.pickingMode = PickingMode.Ignore;
            _body = root.Q<VisualElement>("tooltip-body");
            _arrow = root.Q<VisualElement>("tooltip-arrow");
        }

        public void SetAnchor(VisualElement anchor, TooltipAnchor position = TooltipAnchor.Auto)
        {
            _body.RegisterCallbackOnce<GeometryChangedEvent>(_ =>
            {
                ApplyPosition(anchor.worldBound, position);
            });
        }

        public void SetAnchor(float x, float y, TooltipAnchor position = TooltipAnchor.Auto)
        {
            _body.RegisterCallbackOnce<GeometryChangedEvent>(_ =>
            {
                var anchorBound = new Rect(x, y, 0, 0);
                ApplyPosition(anchorBound, position);
            });
        }

        private void ApplyPosition(Rect anchorBound, TooltipAnchor preferred)
        {
            var tooltipSize = new Vector2(_body.resolvedStyle.width, _body.resolvedStyle.height);
            var containerBound = Root.parent.worldBound;
            var parentOffset = Root.parent.worldBound.position;

            var (pos, resolved) = TooltipPositioner.Calculate(
                anchorBound, tooltipSize, containerBound, preferred);

            _body.style.position = Position.Absolute;
            _body.style.left = pos.x - parentOffset.x;
            _body.style.top = pos.y - parentOffset.y;

            // Set transform-origin toward the anchor for scale effect
            var anchorLocal = new Vector2(
                anchorBound.center.x - pos.x,
                anchorBound.center.y - pos.y);
            _body.style.transformOrigin = new TransformOrigin(
                new Length(anchorLocal.x, LengthUnit.Pixel),
                new Length(anchorLocal.y, LengthUnit.Pixel));

            ApplyArrow(anchorBound, pos, tooltipSize, parentOffset, resolved);
        }

        private void ApplyArrow(Rect anchorBound, Vector2 bodyPos, Vector2 bodySize, Vector2 parentOffset, TooltipAnchor resolved)
        {
            if (_arrow == null) return;

            var arrowW = _arrow.resolvedStyle.width;
            var arrowH = _arrow.resolvedStyle.height;
            float arrowX, arrowY;
            float rotation;

            switch (resolved)
            {
                case TooltipAnchor.Above:
                    arrowX = anchorBound.center.x - arrowW / 2f;
                    arrowY = bodyPos.y + bodySize.y;
                    rotation = 180f;
                    break;
                case TooltipAnchor.Below:
                    arrowX = anchorBound.center.x - arrowW / 2f;
                    arrowY = bodyPos.y - arrowH;
                    rotation = 0f;
                    break;
                case TooltipAnchor.Left:
                    arrowX = bodyPos.x + bodySize.x;
                    arrowY = anchorBound.center.y - arrowH / 2f;
                    rotation = 270f;
                    break;
                case TooltipAnchor.Right:
                    arrowX = bodyPos.x - arrowW;
                    arrowY = anchorBound.center.y - arrowH / 2f;
                    rotation = 90f;
                    break;
                default:
                    return;
            }

            _arrow.style.position = Position.Absolute;
            _arrow.style.left = arrowX - parentOffset.x;
            _arrow.style.top = arrowY - parentOffset.y;
            _arrow.style.rotate = new Rotate(rotation);
        }

        public override void OnShow()
        {
            _isClosing = false;
            Clear(ref _autoHideSchedule);
            base.OnShow();
            DOShowFx();

            if (DismissOnTapOutside)
            {
                _uiSystem.OnScreenTapped += OnScreenTap;
            }
        }

        public override void OnHide()
        {
            _isClosing = false;
            _uiSystem.OnScreenTapped -= OnScreenTap;
            _anim?.Stop();
            _anim = null;
            base.OnHide();
        }

        private void OnScreenTap(Vector2 panelPos)
        {
            var localPos = _body.WorldToLocal(panelPos);
            if (_body.ContainsPoint(localPos)) return;

            Close();
        }

        public void AutoHideIn(float seconds)
        {
            Clear(ref _autoHideSchedule);
            _autoHideSchedule = Root.schedule.Execute(Close)
                .StartingIn((long)(seconds * 1000f));
        }

        private bool _isClosing;

        public void Close()
        {
            if (_isClosing) return;
            _isClosing = true;
            Clear(ref _autoHideSchedule);
            DOHideFx();
        }

        private void DOShowFx()
        {
            _anim?.Stop();

            _body.style.scale = new Scale(Vector3.zero);
            _body.style.opacity = 1;
            if (_arrow != null)
            {
                _arrow.style.scale = new Scale(Vector3.zero);
                _arrow.style.opacity = 1;
            }

            _anim = _body.experimental.animation
                .Start(0f, 1f, 250, (e, v) =>
                {
                    var s = new Scale(new Vector3(v, v, 1));
                    e.style.scale = s;
                    if (_arrow != null) _arrow.style.scale = s;
                })
                .Ease(Easing.OutBack)
                .KeepAlive()
                .OnCompleted(() => _anim = null);
        }

        private void DOHideFx()
        {
            _anim?.Stop();

            _anim = _body.experimental.animation
                .Start(1f, 0f, 150, (e, v) =>
                {
                    var s = new Scale(new Vector3(v, v, 1));
                    e.style.scale = s;
                    if (_arrow != null) _arrow.style.scale = s;
                })
                .Ease(Easing.InSine)
                .KeepAlive()
                .OnCompleted(() => { _anim = null; _uiSystem.Hide(this); });
        }
    }
}
