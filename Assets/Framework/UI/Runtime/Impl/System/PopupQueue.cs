using System;
using System.Collections.Generic;
using Core;

namespace Core.UI
{
    public class PopupQueue : IDisposable
    {
        private class QueueItem
        {
            public Type PopupType { get; }
            public Action ShowAction { get; }
            public int Priority { get; }

            public QueueItem(Type popupType, Action showAction, int priority = 0)
            {
                PopupType = popupType;
                ShowAction = showAction;
                Priority = priority;
            }
        }

        private readonly IUISystem _uiSystem;
        private readonly IEventBus _eventBus;
        private readonly List<QueueItem> _queueItems = new();

        public PopupQueue(IUISystem uiSystem, IEventBus eventBus)
        {
            _uiSystem = uiSystem;
            _eventBus = eventBus;
            _eventBus.Subscribe<UIHiddenEvent>(OnUIHidden);
        }

        public void Dispose()
        {
            _eventBus?.Unsubscribe<UIHiddenEvent>(OnUIHidden);
            _queueItems.Clear();
        }

        private void OnUIHidden(UIHiddenEvent evt)
        {
            CheckShowNextPopupInQueue();
        }

        private void CheckShowNextPopupInQueue()
        {
            if (_uiSystem.GetTopPopup() != null) return;
            if (_queueItems.Count == 0) return;

            // Lowest Priority first; strict '<' keeps FIFO among equal priorities
            // (List.Sort is unstable and would break insertion order here).
            int best = 0;
            for (int i = 1; i < _queueItems.Count; i++)
            {
                if (_queueItems[i].Priority < _queueItems[best].Priority)
                    best = i;
            }

            var nextPopup = _queueItems[best];
            _queueItems.RemoveAt(best);

            nextPopup.ShowAction();
        }

        public void AddToQueue<T>(int priority = 0) where T : IPopup
        {
            _queueItems.Add(new QueueItem(typeof(T), () => _uiSystem.Show<T>(), priority));
            CheckShowNextPopupInQueue();
        }

        public bool HasInQueue<T>() where T : IPopup
        {
            return _queueItems.Exists(item => item.PopupType == typeof(T));
        }

        public bool RemoveFromQueue<T>() where T : IPopup
        {
            return _queueItems.RemoveAll(item => item.PopupType == typeof(T)) > 0;
        }

        public void ClearQueue()
        {
            _queueItems.Clear();
        }
    }
}
