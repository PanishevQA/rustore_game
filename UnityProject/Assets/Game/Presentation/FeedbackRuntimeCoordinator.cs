using System;
using DontGetSidetracked.Core;
using UnityEngine;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Local sensory feedback controlled by versioned SaveData settings.
    /// Sound is procedural (no asset dependency); Android haptics use View.performHapticFeedback,
    /// which follows the user's system haptic setting and needs no VIBRATE permission.
    /// </summary>
    public sealed class FeedbackRuntimeCoordinator : MonoBehaviour
    {
        private const int SampleRate = 44100;
        private const int AndroidVirtualKeyHaptic = 1; // HapticFeedbackConstants.VIRTUAL_KEY, API 5+.

        private static FeedbackRuntimeCoordinator _instance;

        private JsonFileSaveRepository _saveRepository;
        private AudioSource _audioSource;
        private AudioClip _positiveClip;
        private AudioClip _neutralClip;
        private GameBootstrap _bootstrap;
        private bool _wasResult;
        private float _nextPoll;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (FindFirstObjectByType<FeedbackRuntimeCoordinator>() != null) return;
            var root = new GameObject("FeedbackRuntimeCoordinator");
            DontDestroyOnLoad(root);
            root.AddComponent<FeedbackRuntimeCoordinator>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _saveRepository = new JsonFileSaveRepository();
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.loop = false;
            _audioSource.spatialBlend = 0f;
            _audioSource.volume = 0.55f;
            _positiveClip = CreateTone("ResultPositive", 660f, 0.10f);
            _neutralClip = CreateTone("ResultNeutral", 330f, 0.08f);
            ResolveBootstrap();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + 0.05f;
            if (_bootstrap == null) ResolveBootstrap();
            if (_bootstrap == null) return;

            bool isResult = GameBootstrapRuntimeBridge.IsResult(_bootstrap);
            if (!_wasResult && isResult)
                PlayResultFeedback(GameBootstrapRuntimeBridge.LastResultScore(_bootstrap));
            _wasResult = isResult;
        }

        public static void PreviewSound()
        {
            if (_instance == null) return;
            SaveData save = _instance._saveRepository.Load();
            if (save.Settings.Sound) _instance.PlayClip(_instance._positiveClip);
        }

        public static void PreviewHaptic()
        {
            if (_instance == null) return;
            SaveData save = _instance._saveRepository.Load();
            if (save.Settings.Haptics) PerformAndroidHaptic();
        }

        private void PlayResultFeedback(double score)
        {
            SaveData save = _saveRepository.Load();
            if (save.Settings.Sound)
                PlayClip(score >= 90.0 ? _positiveClip : _neutralClip);
            if (save.Settings.Haptics)
                PerformAndroidHaptic();
        }

        private void PlayClip(AudioClip clip)
        {
            if (_audioSource == null || clip == null) return;
            _audioSource.PlayOneShot(clip, 1f);
        }

        private void ResolveBootstrap()
        {
            _bootstrap = FindFirstObjectByType<GameBootstrap>();
            _wasResult = _bootstrap != null && GameBootstrapRuntimeBridge.IsResult(_bootstrap);
        }

        private static AudioClip CreateTone(string name, float frequency, float durationSeconds)
        {
            int sampleCount = Math.Max(1, Mathf.RoundToInt(SampleRate * durationSeconds));
            AudioClip clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            var samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)SampleRate;
                float envelope = 1f - i / (float)sampleCount;
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * 0.35f;
            }
            clip.SetData(samples, 0);
            return clip;
        }

        private static void PerformAndroidHaptic()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var window = activity?.Call<AndroidJavaObject>("getWindow"))
                using (var decor = window?.Call<AndroidJavaObject>("getDecorView"))
                {
                    decor?.Call<bool>("performHapticFeedback", AndroidVirtualKeyHaptic);
                }
            }
            catch (Exception error)
            {
                Debug.Log($"Haptic feedback skipped: {error.Message}");
            }
#endif
        }
    }
}
