#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DontGetSidetracked.EditorTools
{
    /// <summary>
    /// Editor-only helpers for final local QA. Never included in the Android player.
    /// </summary>
    public static class LocalQaTools
    {
        private static readonly string[] LocalFiles =
        {
            "save.json",
            "save.json.bak",
            "save.json.tmp",
            "analytics-local.json",
            "analytics-local.json.tmp",
            "crash-local.log",
            "rustore-remote-config.json",
            "rustore-remote-config.json.bak",
            "rustore-remote-config.json.tmp"
        };

        [MenuItem("Tools/НЕ СБЕЙСЯ!/QA/Open Local Data Folder")]
        public static void OpenLocalDataFolder()
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            EditorUtility.RevealInFinder(Application.persistentDataPath);
        }

        [MenuItem("Tools/НЕ СБЕЙСЯ!/QA/Reset Local Progress and QA Logs")]
        public static void ResetLocalData()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(
                    "НЕ СБЕЙСЯ! QA",
                    "Остановите Play Mode перед сбросом локальных данных.",
                    "OK");
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "Сбросить локальные данные?",
                "Будут удалены save, backup, локальная аналитика, crash-log и cache Remote Config для Editor. Это действие нельзя отменить.",
                "СБРОСИТЬ",
                "ОТМЕНА");
            if (!confirmed) return;

            int deleted = 0;
            for (int i = 0; i < LocalFiles.Length; i++)
            {
                string path = Path.Combine(Application.persistentDataPath, LocalFiles[i]);
                if (!File.Exists(path)) continue;
                try
                {
                    File.Delete(path);
                    deleted++;
                }
                catch (Exception error)
                {
                    Debug.LogWarning($"QA cleanup could not delete {path}: {error.Message}");
                }
            }

            Debug.Log($"НЕ СБЕЙСЯ! QA: deleted {deleted} local file(s). Next Play Mode starts from a clean local state.");
        }

        [MenuItem("Tools/НЕ СБЕЙСЯ!/QA/Reset Local Progress and QA Logs", true)]
        private static bool ValidateResetLocalData() => !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}
#endif
