using System;
using System.Collections.Generic;
using System.IO;
using DontGetSidetracked.Core;
using UnityEngine;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Versioned JSON save with a process-wide in-memory identity per file path.
    /// Runtime services that independently construct a repository therefore still mutate the same SaveData instance,
    /// preventing stale copies from overwriting purchases, campaign rewards, hints or settings later in the session.
    /// </summary>
    public sealed class JsonFileSaveRepository : ISaveRepository
    {
        private static readonly object SharedGate = new object();
        private static readonly Dictionary<string, SaveData> SharedByPath = new Dictionary<string, SaveData>(StringComparer.Ordinal);

        private readonly string _path;
        private readonly string _backupPath;

        public JsonFileSaveRepository(string fileName = "save.json")
        {
            _path = Path.Combine(Application.persistentDataPath, fileName);
            _backupPath = _path + ".bak";
        }

        public SaveData Load()
        {
            lock (SharedGate)
            {
                if (SharedByPath.TryGetValue(_path, out SaveData shared) && shared != null)
                    return shared;

                SaveData loaded = TryLoad(_path) ?? TryLoad(_backupPath) ?? SaveData.CreateNew();
                loaded = SaveMigrator.Migrate(loaded);
                SharedByPath[_path] = loaded;
                return loaded;
            }
        }

        public void Save(SaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            lock (SharedGate)
            {
                data = SaveMigrator.Migrate(data);
                SharedByPath[_path] = data;

                string tempPath = _path + ".tmp";
                File.WriteAllText(tempPath, JsonUtility.ToJson(data, false));

                if (File.Exists(_path))
                {
                    try { File.Copy(_path, _backupPath, true); }
                    catch (IOException) { }
                    File.Delete(_path);
                }
                File.Move(tempPath, _path);
            }
        }

        private static SaveData TryLoad(string path)
        {
            if (!File.Exists(path)) return null;
            try { return JsonUtility.FromJson<SaveData>(File.ReadAllText(path)); }
            catch (Exception error)
            {
                Debug.LogWarning($"Save load failed for {path}: {error.Message}");
                return null;
            }
        }
    }
}
