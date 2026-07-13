using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Core.UI
{
    /// <summary>
    /// Swipeable page view with dot indicators.
    ///
    /// USS class names (BEM):
    ///   .page-view                — root container
    ///   .page-view__viewport      — clipping area
    ///   .page-view__track         — horizontal page strip
    ///   .page-view__page          — each page (auto-added via AddPage)
    ///   .page-view__dots          — dot indicator container
    ///   .page-view__dot           — inactive dot
    ///   .page-view__dot--active   — active dot (current page)
    /// </summary>
    [UxmlElement]
    public partial class PageView : VisualElement
    {
        public event Action<int> OnPageChanged;

        private VisualElement _viewport;
        private VisualElement _track;
        private VisualElement _dotContainer;
        private readonly List<VisualElement> _pages = new();

        private int _currentPage;
        private float _pageWidth;

        // drag
        private bool _dragging;
        private float _dragStartX;
        private float _dragOffsetX;
        private float _trackStartX;

        [UxmlAttribute]
        public bool showDots { get; set; } = true;

        public int CurrentPage => _currentPage;
        public int PageCount => _pages.Count;

        public PageView()
        {
            AddToClassList("page-view");

            _viewport = new VisualElement();
            _viewport.AddToClassList("page-view__viewport");
            hierarchy.Add(_viewport);

            _track = new VisualElement();
            _track.AddToClassList("page-view__track");
            _viewport.Add(_track);

            _dotContainer = new VisualElement();
            _dotContainer.AddToClassList("page-view__dots");
            hierarchy.Add(_dotContainer);

            _viewport.RegisterCallback<PointerDownEvent>(OnDragStart);
            _viewport.RegisterCallback<PointerMoveEvent>(OnDragMove);
            _viewport.RegisterCallback<PointerUpEvent>(OnDragEnd);
            _viewport.RegisterCallback<PointerLeaveEvent>(e => FinishDrag());

            _viewport.RegisterCallback<GeometryChangedEvent>(e =>
            {
                _pageWidth = _viewport.resolvedStyle.width;
                foreach (var page in _pages)
                    page.style.width = _pageWidth;
                SnapToPage(_currentPage);
            });
        }

        public void AddPage(VisualElement page)
        {
            page.AddToClassList("page-view__page");
            _pages.Add(page);
            _track.Add(page);
            RebuildDots();
        }

        public void RemovePage(VisualElement page)
        {
            if (!_pages.Remove(page)) return;
            _track.Remove(page);
            RebuildDots();
            if (_currentPage >= _pages.Count)
                GoTo(_pages.Count - 1);
        }

        public void ClearPages()
        {
            _pages.Clear();
            _track.Clear();
            _dotContainer.Clear();
            _currentPage = 0;
        }

        public void GoTo(int index)
        {
            var prev = _currentPage;
            _currentPage = Mathf.Clamp(index, 0, Mathf.Max(0, _pages.Count - 1));
            SnapToPage(_currentPage);
            if (prev != _currentPage)
                OnPageChanged?.Invoke(_currentPage);
        }

        public void Next() => GoTo(_currentPage + 1);
        public void Previous() => GoTo(_currentPage - 1);

        private void SnapToPage(int index)
        {
            if (_pageWidth <= 0) return;
            _track.style.transitionDuration = new StyleList<TimeValue>(
                new List<TimeValue> { new(300, TimeUnit.Millisecond) });
            _track.style.translate = new Translate(-index * _pageWidth, 0);
            UpdateDots();
        }

        private void RebuildDots()
        {
            _dotContainer.Clear();
            _dotContainer.style.display = showDots ? DisplayStyle.Flex : DisplayStyle.None;

            for (int i = 0; i < _pages.Count; i++)
            {
                var dot = new VisualElement();
                dot.AddToClassList("page-view__dot");

                var idx = i;
                dot.RegisterCallback<ClickEvent>(e => GoTo(idx));

                _dotContainer.Add(dot);
            }
            UpdateDots();
        }

        private void UpdateDots()
        {
            for (int i = 0; i < _dotContainer.childCount; i++)
            {
                var dot = _dotContainer[i];
                if (i == _currentPage)
                    dot.AddToClassList("page-view__dot--active");
                else
                    dot.RemoveFromClassList("page-view__dot--active");
            }
        }

        private void OnDragStart(PointerDownEvent evt)
        {
            if (_pages.Count <= 1) return;

            _dragging = true;
            _dragStartX = evt.position.x;
            _dragOffsetX = 0;
            _trackStartX = -_currentPage * _pageWidth;

            _track.style.transitionDuration = new StyleList<TimeValue>(
                new List<TimeValue> { new(0, TimeUnit.Millisecond) });

            _viewport.CapturePointer(evt.pointerId);
        }

        private void OnDragMove(PointerMoveEvent evt)
        {
            if (!_dragging) return;

            _dragOffsetX = evt.position.x - _dragStartX;
            var x = _trackStartX + _dragOffsetX;

            var min = -(_pages.Count - 1) * _pageWidth;
            x = Mathf.Clamp(x, min, 0);

            _track.style.translate = new Translate(x, 0);
        }

        private void OnDragEnd(PointerUpEvent evt)
        {
            if (!_dragging) return;
            if (_viewport.HasPointerCapture(evt.pointerId))
                _viewport.ReleasePointer(evt.pointerId);
            FinishDrag();
        }

        private void FinishDrag()
        {
            if (!_dragging) return;
            _dragging = false;

            var threshold = _pageWidth * 0.2f;
            if (_dragOffsetX < -threshold)
                GoTo(_currentPage + 1);
            else if (_dragOffsetX > threshold)
                GoTo(_currentPage - 1);
            else
                SnapToPage(_currentPage);
        }
    }
}
