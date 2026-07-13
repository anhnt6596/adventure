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
            public object Data { get; }
            public Action ShowAction { get; }
            public int Priority { get; }

            public QueueItem(Type popupType, object data, Action showAction, int priority = 0)
            {
                PopupType = popupType;
                Data = data;
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

            _queueItems.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            var nextPopup = _queueItems[0];
            _queueItems.RemoveAt(0);

            nextPopup.ShowAction();
        }

        public void AddToQueue<T>(int priority = 0) where T : IPopup
        {
            _queueItems.Add(new QueueItem(typeof(T), null, () => _uiSystem.Show<T>(), priority));
            CheckShowNextPopupInQueue();
        }

        public void AddToQueue<T, TData>(TData data, int priority = 0) where T : IPopup, IWithData<TData>
        {
            _queueItems.Add(new QueueItem(typeof(T), data, () => _uiSystem.Show<T, TData>(data), priority));
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
