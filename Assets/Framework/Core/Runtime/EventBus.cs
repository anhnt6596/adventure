using System;
using System.Collections.Generic;

namespace Core
{
    public class EventBus : IEventBus
    {
        private readonly Dictionary<Type, Delegate> _handlers = new();

        public void Subscribe<T>(Action<T> handler)
        {
            if (handler == null) return;
            var t = typeof(T);
            _handlers[t] = _handlers.TryGetValue(t, out var d)
                ? (Action<T>)d + handler
                : handler;
        }

        public void Unsubscribe<T>(Action<T> handler)
        {
            if (handler == null) return;
            var t = typeof(T);
            if (!_handlers.TryGetValue(t, out var d)) return;

            var current = (Action<T>)d - handler;
            if (current == null) _handlers.Remove(t);
            else _handlers[t] = current;
        }

        public void Publish<T>(T evt)
        {
            if (!_handlers.TryGetValue(typeof(T), out var d)) return;

            foreach (var handler in d.GetInvocationList())
            {
                try
                {
                    ((Action<T>)handler).Invoke(evt);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogException(e);
                }
            }
        }
    }
}
