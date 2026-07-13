using System;
using System.IO;
using UnityEngine;

namespace Core.Save
{
    public sealed class SaveFileStore
    {
        readonly ISaveSerializer _serializer;
        public string Path { get; }
        public SaveBag Data { get; private set; }
        public bool IsFresh { get; private set; }

        public SaveFileStore(string path, ISaveSerializer serializer)
        {
            Path = path;
            _serializer = serializer;
            Load();
        }

        void Load()
        {
            var bak = Path + ".bak";
            if (File.Exists(bak) && !File.Exists(Path))
            {
                Debug.LogWarning($"[Save] recovering '{Path}' from backup");
                File.Move(bak, Path);
            }
            if (!File.Exists(Path)) { Data = new SaveBag(); IsFresh = true; return; }
            try
            {
                using var stream = new FileStream(Path, FileMode.Open, FileAccess.Read);
                Data = _serializer.Deserialize(stream) as SaveBag;
            }
            catch (Exception e) { Debug.LogError($"[Save] load '{Path}' failed: {e.Message}"); }
            if (Data == null) { Data = new SaveBag(); IsFresh = true; }
        }

        public void Save()
        {
            var dir = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var tmp = Path + ".tmp";
            var bak = Path + ".bak";
            try
            {
                using (var stream = new FileStream(tmp, FileMode.Create, FileAccess.Write))
                    _serializer.Serialize(Data, stream);
                if (File.Exists(Path)) { if (File.Exists(bak)) File.Delete(bak); File.Move(Path, bak); }
                File.Move(tmp, Path);
                if (File.Exists(bak)) File.Delete(bak);
                IsFresh = false;
            }
            catch (Exception e) { Debug.LogError($"[Save] save '{Path}' failed: {e.Message}"); throw; }
        }

        public void ClearOnDisk()
        {
            Data.Clear();
            foreach (var p in new[] { Path, Path + ".bak", Path + ".tmp" })
                if (File.Exists(p)) File.Delete(p);
            IsFresh = true;
        }
    }
}
