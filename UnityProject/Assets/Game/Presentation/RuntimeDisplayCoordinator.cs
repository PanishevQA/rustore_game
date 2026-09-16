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
