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
    /// The shared cache is reset at runtime subsystem registration so Editor Play sessions without domain reload
    /// still reload the persisted file instead of reusing stale in-memory state.
    /// </summary>
    public sealed class JsonFileSaveRepository : ISaveRepository
    {
        private static readonly object SharedGate = new object();
        private static readonly Dictionary<string, SaveData> SharedByPath = new Dictionary<string, SaveData>(StringComparer.Ordinal);

        private readonly string _path;
        private readonly string _backupPath;
        private readonly string _tempPath;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSharedState()
        {
            lock (SharedGate)
                SharedByPath.Clear();
        }

        public JsonFileSaveRepository(string fileName = "save.json")
        {
            _path = Path.Combine(Application.persistentDataPath, fileName);
            _backupPath = _path + ".bak";
            _tempPath = _path + ".tmp";
        }

        public SaveData Load()
        {
            lock (SharedGate)
            {
                if (SharedByPath.TryGetValue(_path, out SaveData shared) && shared != null)
                    return shared;

                SaveData primary = TryLoad(_path);
                SaveData interruptedWrite = TryLoad(_tempPath);
                SaveData loaded = primary;

                if (interruptedWrite != null && ShouldRecoverInterruptedWrite(primary))
                {
                    loaded = interruptedWrite;
                    PromoteInterruptedWrite(preservePrimaryAsBackup: primary != null);
                }
                else if (File.Exists(_tempPath))
                {
                    // A stale or malformed temp file must never shadow a known-good primary on future launches.
                    TryDelete(_tempPath);
                }

                if (loaded == null)
                {
                    SaveData backup = TryLoad(_backupPath);
                    if (backup != null)
                    {
                        loaded = backup;
                        RestorePrimaryFromBackup(overwriteExisting: true);
                    }
                    else
                    {
                        // Neither file is usable. Do not let an unreadable primary become the next "good" backup.
                        TryDelete(_path);
                        TryDelete(_backupPath);
                        loaded = SaveData.CreateNew();
                    }
                }

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
                File.WriteAllText(_tempPath, JsonUtility.ToJson(data, false));

                try
                {
                    if (File.Exists(_path))
                    {
                        // Never remove the current primary save unless its backup was created successfully.
                        File.Copy(_path, _backupPath, true);
                        File.Delete(_path);
                    }

                    File.Move(_tempPath, _path);
                    SharedByPath[_path] = data;
                }
                catch
                {
                    // If replacement failed after the primary was removed, immediately restore the known-good backup.
                    RestorePrimaryFromBackup(overwriteExisting: false);
                    TryDelete(_tempPath);
                    throw;
                }
            }
        }

        private bool ShouldRecoverInterruptedWrite(SaveData primary)
        {
            if (primary == null) return true;
            if (!File.Exists(_tempPath) || !File.Exists(_path)) return false;

            try
            {
                return File.GetLastWriteTimeUtc(_tempPath) > File.GetLastWriteTimeUtc(_path);
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Save temp timestamp check failed for {_path}: {error.Message}");
                return false;
            }
        }

        private void PromoteInterruptedWrite(bool preservePrimaryAsBackup)
        {
            try
            {
                if (preservePrimaryAsBackup && File.Exists(_path))
                    File.Copy(_path, _backupPath, true);

                File.Copy(_tempPath, _path, true);
                TryDelete(_tempPath);
            }
            catch (Exception error)
            {
                // The recovered object is still kept in memory and the next successful Save will persist it.
                Debug.LogWarning($"Interrupted save promotion failed for {_path}: {error.Message}");
            }
        }

        private void RestorePrimaryFromBackup(bool overwriteExisting)
        {
            if (!File.Exists(_backupPath)) return;
            if (!overwriteExisting && File.Exists(_path)) return;
            try
            {
                File.Copy(_backupPath, _path, true);
            }
            catch (Exception error)
            {
                Debug.LogError($"Save recovery failed for {_path}: {error.Message}");
            }
        }

        private static void TryDelete(string path)
        {
            if (!File.Exists(path)) return;
            try { File.Delete(path); }
            catch (Exception) { }
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
