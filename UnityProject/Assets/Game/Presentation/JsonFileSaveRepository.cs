using System;
using System.IO;
using DontGetSidetracked.Core;
using UnityEngine;

namespace DontGetSidetracked.Presentation
{
    public sealed class JsonFileSaveRepository : ISaveRepository
    {
        private readonly string _path;
        private readonly string _backupPath;

        public JsonFileSaveRepository(string fileName = "save.json")
        {
            _path = Path.Combine(Application.persistentDataPath, fileName);
            _backupPath = _path + ".bak";
        }

        public SaveData Load()
        {
            SaveData loaded = TryLoad(_path) ?? TryLoad(_backupPath) ?? SaveData.CreateNew();
            return SaveMigrator.Migrate(loaded);
        }

        public void Save(SaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            data = SaveMigrator.Migrate(data);
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
