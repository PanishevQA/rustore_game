using UnityEngine;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Lightweight unscaled-time entrance motion for release panels.
    /// Presentation-only: no gameplay state, input ownership or persistence.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class ReleasePanelMotion : MonoBehaviour
    {
        [SerializeField] private float _duration = 0.18f;
        [SerializeField] private float _startScale = 0.965f;

        private CanvasGroup _group;
        private float _elapsed;
        private bool _animating;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
        }

        private void OnEnable()
        {
            Play();
        }

        public void Play()
        {
            if (_group == null)
            {
                _group = GetComponent<CanvasGroup>();
                if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            }

            _elapsed = 0f;
            _animating = true;
            _group.alpha = 0f;
            transform.localScale = Vector3.one * Mathf.Clamp(_startScale, 0.90f, 1f);
        }

        private void Update()
        {
            if (!_animating) return;

            _elapsed += Time.unscaledDeltaTime;
            float duration = Mathf.Max(0.05f, _duration);
            float t = Mathf.Clamp01(_elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            _group.alpha = eased;
            float scale = Mathf.Lerp(Mathf.Clamp(_startScale, 0.90f, 1f), 1f, eased);
            transform.localScale = Vector3.one * scale;

            if (t < 1f) return;
            _group.alpha = 1f;
            transform.localScale = Vector3.one;
            _animating = false;
        }

        private void OnDisable()
        {
            if (_group != null) _group.alpha = 1f;
            transform.localScale = Vector3.one;
            _animating = false;
        }
    }
}
