using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Core.Save
{
    [Serializable, JsonObject(MemberSerialization.OptIn)]
    public sealed class SaveBag : IEnumerable<KeyValuePair<string, object>>
    {
        [JsonProperty] Dictionary<string, object> _data = new Dictionary<string, object>();

        public bool ContainsKey(string key) => _data.ContainsKey(key);
        public void Remove(string key) => _data.Remove(key);
        public void Clear() => _data.Clear();
        public void Set(string key, object value) => _data[key] = value;

        public T Get<T>(string key, T fallback = default)
            => _data.TryGetValue(key, out var v) ? Coerce(v, fallback) : fallback;

        public bool TryGet<T>(string key, out T value)
        {
            value = default;
            if (!_data.TryGetValue(key, out var v)) return false;
            value = Coerce(v, default(T));
            return true;
        }

        public SaveBag Child(string key)
        {
            if (_data.TryGetValue(key, out var v) && v is SaveBag b) return b;
            var bag = new SaveBag();
            _data[key] = bag;
            return bag;
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => _data.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        static T Coerce<T>(object input, T fallback)
        {
            if (input is T t) return t;
            if (input == null) return fallback;
            try
            {
                var type = typeof(T);
                if (type.IsEnum) return (T)Enum.ToObject(type, Convert.ToInt64(input));
                return (T)Convert.ChangeType(input, type);
            }
            catch { return fallback; }
        }
    }
}
