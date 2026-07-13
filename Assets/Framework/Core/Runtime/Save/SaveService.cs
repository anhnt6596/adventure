using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Core.Save
{
    public sealed class SaveService : IDisposable
    {
        readonly ISaveSerializer _serializer;
        readonly Dictionary<string, SaveFileStore> _stores = new Dictionary<string, SaveFileStore>();
        readonly List<ISavable> _registered = new List<ISavable>();

        public SaveService(ISaveSerializer serializer = null)
        {
            _serializer = serializer ?? new JsonSaveSerializer();
        }

        SaveFileStore Store(string key)
        {
            if (!_stores.TryGetValue(key, out var s))
            {
                var path = Path.Combine(Application.persistentDataPath, key + ".json");
                s = new SaveFileStore(path, _serializer);
                _stores[key] = s;
            }
            return s;
        }

        public void Register(ISavable savable)
        {
            if (!_registered.Contains(savable)) _registered.Add(savable);
            savable.Load(Store(savable.SaveKey).Data);
        }

        public void Capture(ISavable savable) => savable.Save(Store(savable.SaveKey).Data);

        public void Save(string key)
        {
            var store = Store(key);
            foreach (var s in _registered) if (s.SaveKey == key) s.Save(store.Data);
            store.Save();
        }

        public void SaveAll()
        {
            foreach (var s in _registered) s.Save(Store(s.SaveKey).Data);
            foreach (var kv in _stores) kv.Value.Save();
        }

        public void Clear(string key) => Store(key).ClearOnDisk();

        public void ClearAll()
        {
            foreach (var kv in _stores) kv.Value.ClearOnDisk();
        }

        public void Dispose() => SaveAll();
    }
}
