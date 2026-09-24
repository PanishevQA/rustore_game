using System;
using System.IO;
using UnityEngine;

namespace DontGetSidetracked.Presentation
{
    public static class NativeImageShare
    {
        public static string SaveResultCard(byte[] pngBytes)
        {
            if (pngBytes == null || pngBytes.Length == 0) throw new ArgumentException("PNG data is empty.", nameof(pngBytes));
            string directory = Path.Combine(Application.temporaryCachePath, "share");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "nesbeisya_result.png");
            File.WriteAllBytes(path, pngBytes);
            return path;
        }

        public static bool Share(byte[] pngBytes, string text)
        {
            string path = SaveResultCard(pngBytes);
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var file = new AndroidJavaObject("java.io.File", path))
                using (var provider = new AndroidJavaClass("androidx.core.content.FileProvider"))
                using (var uri = provider.CallStatic<AndroidJavaObject>(
                           "getUriForFile",
                           activity,
                           Application.identifier + ".shareprovider",
                           file))
                using (var intent = new AndroidJavaObject("android.content.Intent"))
                using (var intentClass = new AndroidJavaClass("android.content.Intent"))
                {
                    intent.Call<AndroidJavaObject>("setAction", "android.intent.action.SEND");
                    intent.Call<AndroidJavaObject>("setType", "image/png");
                    intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.TEXT", text ?? string.Empty);
                    intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.STREAM", uri);
                    intent.Call<AndroidJavaObject>("addFlags", 1); // FLAG_GRANT_READ_URI_PERMISSION
                    using (var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, "Поделиться результатом"))
                    {
                        activity.Call("startActivity", chooser);
                    }
                }
                return true;
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Image share failed, falling back to text: {error.Message}");
                ShareText(text);
                return false;
            }
#else
            Debug.Log($"Result share card: {path}\n{text}");
            return true;
#endif
        }

        private static void ShareText(string text)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var intent = new AndroidJavaObject("android.content.Intent"))
                using (var intentClass = new AndroidJavaClass("android.content.Intent"))
                {
                    intent.Call<AndroidJavaObject>("setAction", "android.intent.action.SEND");
                    intent.Call<AndroidJavaObject>("setType", "text/plain");
                    intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.TEXT", text ?? string.Empty);
                    using (var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, "Поделиться результатом"))
                    {
                        activity.Call("startActivity", chooser);
                    }
                }
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Text share failed: {error.Message}");
            }
#else
            Debug.Log(text);
#endif
        }
    }
}
