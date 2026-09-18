using UnityEngine;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Ensures the programmatic ScreenSpaceOverlay UI still has a valid Game View render target
    /// and an audio listener when the bootstrap scene itself is intentionally empty.
    /// </summary>
    public sealed class RuntimeDisplayCoordinator : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureDisplayInfrastructure()
        {
            // The interaction is a one-finger precision gesture; keep frame pacing predictable
            // and ignore extra touches before any gameplay state is created.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            Input.multiTouchEnabled = false;

            Camera camera = FindFirstObjectByType<Camera>();
            if (camera == null)
            {
                var go = new GameObject("RuntimeCamera", typeof(Camera));
                DontDestroyOnLoad(go);
                camera = go.GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.008f, 0.012f, 0.035f, 1f);
                camera.cullingMask = 0;
                camera.depth = -100f;
                camera.targetDisplay = 0;
                camera.orthographic = true;
            }

            if (FindFirstObjectByType<AudioListener>() == null)
                camera.gameObject.AddComponent<AudioListener>();
        }
    }
}
