#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace DontGetSidetracked.EditorTools
{
    /// <summary>
    /// Machine-local production signing readiness.
    /// Checks only presence/shape; secret values are never logged or persisted.
    /// </summary>
    internal static class ProductionSigningRuntimeValidator
    {
        internal const string KeystorePasswordEnvironmentVariable = "NESBEISYA_KEYSTORE_PASS";
        internal const string KeyAliasPasswordEnvironmentVariable = "NESBEISYA_KEYALIAS_PASS";
        private const string InProjectPrefix = "{inproject}:";

        internal static List<string> CollectErrors()
        {
            var errors = new List<string>();

            if (!PlayerSettings.Android.useCustomKeystore)
            {
                errors.Add("Production signing runtime requires a custom Android keystore.");
                return errors;
            }

            string configuredKeystore = PlayerSettings.Android.keystoreName ?? string.Empty;
            if (string.IsNullOrWhiteSpace(configuredKeystore))
            {
                errors.Add("Production signing runtime has no configured keystore path.");
            }
            else
            {
                try
                {
                    string resolved = ResolveKeystorePath(configuredKeystore);
                    if (string.IsNullOrWhiteSpace(resolved) || !File.Exists(resolved))
                    {
                        errors.Add(
                            "Production keystore file is not available on this machine for the configured " +
                            "PlayerSettings.Android.keystoreName. Keep the keystore local and copy it to the " +
                            "configured release-runner location before Build mode.");
                    }
                }
                catch (Exception error)
                {
                    errors.Add("Production keystore path could not be resolved: " + error.Message);
                }
            }

            if (string.IsNullOrWhiteSpace(PlayerSettings.Android.keyaliasName))
                errors.Add("Production signing runtime has no Android key alias.");

            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(KeystorePasswordEnvironmentVariable)))
            {
                errors.Add(
                    $"Production signing environment variable {KeystorePasswordEnvironmentVariable} is not available " +
                    "to the Unity process.");
            }

            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(KeyAliasPasswordEnvironmentVariable)))
            {
                errors.Add(
                    $"Production signing environment variable {KeyAliasPasswordEnvironmentVariable} is not available " +
                    "to the Unity process.");
            }

            return errors;
        }

        internal static void EnsureReady()
        {
            List<string> errors = CollectErrors();
            if (errors.Count == 0) return;

            throw new BuildFailedException(
                "Production signing runtime preflight failed:\n- " + string.Join("\n- ", errors));
        }

        internal static string ResolveKeystorePath(string configured)
        {
            if (string.IsNullOrWhiteSpace(configured)) return string.Empty;

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                ?? Directory.GetCurrentDirectory();
            string candidate = configured.Trim();

            if (candidate.StartsWith(InProjectPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string relative = candidate.Substring(InProjectPrefix.Length).Trim();
                relative = relative.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return Path.GetFullPath(Path.Combine(projectRoot, relative));
            }

            if (Path.IsPathRooted(candidate))
                return Path.GetFullPath(candidate);

            return Path.GetFullPath(Path.Combine(projectRoot, candidate));
        }
    }
}
#endif
