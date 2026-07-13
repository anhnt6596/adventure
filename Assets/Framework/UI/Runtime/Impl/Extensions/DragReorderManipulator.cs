using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Core.UI
{
    /// <summary>
    /// Attach to a container to make its children reorderable by drag.
    ///
    /// Usage:
    ///   container.AddManipulator(new DragReorderManipulator(onReorder));
    ///
    /// Callback (oldIndex, newIndex) fires after a successful reorder.
    /// </summary>
    public class DragReorderManipulator : PointerManipulator
    {
        private readonly Action<int, int> _onReorder;

        private VisualElement _dragItem;
        private VisualElement _placeholder;
        private int _dragIndex;
        private float _dragOffsetY;
        private int _pointerId = -1;
        private readonly List<float> _childMidpoints = new();

        public DragReorderManipulator(Action<int, int> onReorder = null)
        {
            _onReorder = onReorder;
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
            if (evt.button != 0) return;
            if (_dragItem != null) return;

            // find which direct child was clicked
            var child = FindDirectChild(evt.position);
            if (child == null) return;

            _dragIndex = target.IndexOf(child);
            _dragItem = child;
            _pointerId = evt.pointerId;

            var childRect = child.worldBound;
            _dragOffsetY = evt.position.y - childRect.y;

            // create placeholder to hold the space
            _placeholder = new VisualElement();
            _placeholder.style.height = childRect.height;
            _placeholder.style.marginTop = child.resolvedStyle.marginTop;
            _placeholder.style.marginBottom = child.resolvedStyle.marginBottom;
            _placeholder.style.opacity = 0.3f;
            _placeholder.style.backgroundColor = new Color(0.13f, 0.59f, 0.95f, 0.2f);
            _placeholder.style.borderTopLeftRadius = 8;
            _placeholder.style.borderTopRightRadius = 8;
            _placeholder.style.borderBottomLeftRadius = 8;
            _placeholder.style.borderBottomRightRadius = 8;

            // switch to absolute positioning
            target.Insert(_dragIndex, _placeholder);
            child.style.position = Position.Absolute;
            child.style.width = childRect.width;
            child.style.left = 0;
            child.style.top = evt.position.y - target.worldBound.y - _dragOffsetY;
            child.style.opacity = 0.9f;
            child.BringToFront();

            target.CapturePointer(_pointerId);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (_dragItem == null) return;

            var containerRect = target.worldBound;
            var y = evt.position.y - containerRect.y - _dragOffsetY;
            y = Mathf.Clamp(y, 0, containerRect.height - _dragItem.resolvedStyle.height);
            _dragItem.style.top = y;

            // find new index based on midpoints
            UpdateMidpoints();
            var pointerY = evt.position.y;
            var newIndex = _childMidpoints.Count;

            for (int i = 0; i < _childMidpoints.Count; i++)
            {
                if (pointerY < _childMidpoints[i])
                {
                    newIndex = i;
                    break;
                }
            }

            // move placeholder
            var currentPlaceholderIndex = target.IndexOf(_placeholder);
            if (newIndex != currentPlaceholderIndex)
            {
                _placeholder.RemoveFromHierarchy();
                if (newIndex >= target.childCount)
                    target.Add(_placeholder);
                else
                    target.Insert(newIndex, _placeholder);
            }
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (_dragItem == null) return;
            if (evt.pointerId != _pointerId) return;

            FinishDrag();
        }

        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            if (_dragItem == null) return;
            FinishDrag();
        }

        private void FinishDrag()
        {
            if (_dragItem == null) return;

            var newIndex = target.IndexOf(_placeholder);

            // reset styles
            _dragItem.style.position = StyleKeyword.Null;
            _dragItem.style.width = StyleKeyword.Null;
            _dragItem.style.left = StyleKeyword.Null;
            _dragItem.style.top = StyleKeyword.Null;
            _dragItem.style.opacity = StyleKeyword.Null;

            // replace placeholder with the item
            _placeholder.RemoveFromHierarchy();
            _dragItem.RemoveFromHierarchy();

            if (newIndex >= target.childCount)
                target.Add(_dragItem);
            else
                target.Insert(newIndex, _dragItem);

            if (target.HasPointerCapture(_pointerId))
                target.ReleasePointer(_pointerId);

            if (_dragIndex != newIndex)
                _onReorder?.Invoke(_dragIndex, newIndex);

            _dragItem = null;
            _placeholder = null;
            _pointerId = -1;
        }

        private VisualElement FindDirectChild(Vector3 pointerPos)
        {
            for (int i = 0; i < target.childCount; i++)
            {
                var child = target[i];
                if (child.worldBound.Contains(pointerPos))
                    return child;
            }
            return null;
        }

        private void UpdateMidpoints()
        {
            _childMidpoints.Clear();
            for (int i = 0; i < target.childCount; i++)
            {
                var child = target[i];
                if (child == _dragItem) continue;
                var rect = child.worldBound;
                _childMidpoints.Add(rect.y + rect.height * 0.5f);
            }
        }
    }
}
