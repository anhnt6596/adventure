using UnityEngine;
using UnityEngine.UIElements;

namespace Core.UI
{
    public class DragScrollManipulator : PointerManipulator
    {
        private const float DragThreshold = 5f;

        private ScrollView _scrollView;
        private bool _isTracking;
        private bool _isDragging;
        private int _trackingPointerId;
        private Vector2 _startPointer;
        private Vector2 _startOffset;

        public DragScrollManipulator(ScrollView scrollView)
        {
            _scrollView = scrollView;
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            target.UnregisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
            target.UnregisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            target.UnregisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (!CanStartManipulation(evt)) return;

            _isTracking = true;
            _isDragging = false;
            _trackingPointerId = evt.pointerId;
            _startPointer = evt.position;
            _startOffset = _scrollView.scrollOffset;
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_isTracking || evt.pointerId != _trackingPointerId) return;

            var delta = (Vector2)evt.position - _startPointer;

            if (!_isDragging)
            {
                if (delta.magnitude < DragThreshold) return;

                _isDragging = true;
                target.CapturePointer(evt.pointerId);
            }

            _scrollView.scrollOffset = _startOffset - delta;
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!_isTracking || evt.pointerId != _trackingPointerId) return;

            var wasDragging = _isDragging;
            _isTracking = false;
            _isDragging = false;

            if (wasDragging)
            {
                target.ReleasePointer(evt.pointerId);
                evt.StopPropagation();
            }
        }

        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            _isTracking = false;
            _isDragging = false;
        }
    }
}
