using UnityEngine;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Ensures the programmatic ScreenSpaceOverlay UI still has a valid Game View render target
    /// and exactly one audio listener when the bootstrap scene itself is intentionally empty.
    /// </summary>
    public sealed class RuntimeDisplayCoordinator : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureDisplayInfrastructure()
        {
            if (FindFirstObjectByType<Camera>() != null)
                return;

            var go = new GameObject("RuntimeCamera", typeof(Camera), typeof(AudioListener));
            DontDestroyOnLoad(go);

            Camera camera = go.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.008f, 0.012f, 0.035f, 1f);
            camera.cullingMask = 0;
            camera.depth = -100f;
            camera.targetDisplay = 0;
            camera.orthographic = true;
        }
    }
}
