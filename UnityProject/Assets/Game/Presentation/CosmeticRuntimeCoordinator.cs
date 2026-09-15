using System;
using System.Reflection;
using DontGetSidetracked.Core;
using DontGetSidetracked.Economy;
using UnityEngine;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Applies the locally selected trail cosmetic only during Drawing.
    /// Result colors remain score-driven in GameBootstrap for readability.
    /// </summary>
    public sealed class CosmeticRuntimeCoordinator : MonoBehaviour
    {
        private JsonFileSaveRepository _saveRepository;
        private GameBootstrap _bootstrap;
        private FieldInfo _stateField;
        private FieldInfo _playerGraphicField;
        private float _nextPoll;
        private string _lastSkin = string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (FindFirstObjectByType<CosmeticRuntimeCoordinator>() != null) return;
            var root = new GameObject("CosmeticRuntimeCoordinator");
            DontDestroyOnLoad(root);
            root.AddComponent<CosmeticRuntimeCoordinator>();
        }

        private void Awake()
        {
            _saveRepository = new JsonFileSaveRepository();
            ResolveBootstrap();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + 0.2f;
            if (_bootstrap == null) ResolveBootstrap();
            if (_bootstrap == null || _stateField == null || _playerGraphicField == null) return;

            string state = _stateField.GetValue(_bootstrap)?.ToString() ?? string.Empty;
            if (!string.Equals(state, "Drawing", StringComparison.Ordinal)) return;

            SaveData save = _saveRepository.Load();
            var selection = new CosmeticSelectionService(_saveRepository, save);
            string skin = selection.SelectedSkinId;
            RouteGraphic player = _playerGraphicField.GetValue(_bootstrap) as RouteGraphic;
            if (player == null) return;

            Color desired = ColorFor(skin);
            if (!string.Equals(_lastSkin, skin, StringComparison.Ordinal) || player.color != desired)
            {
                _lastSkin = skin;
                player.color = desired;
            }
        }

        private void ResolveBootstrap()
        {
            _bootstrap = FindFirstObjectByType<GameBootstrap>();
            if (_bootstrap == null)
            {
                _stateField = null;
                _playerGraphicField = null;
                return;
            }

            Type type = typeof(GameBootstrap);
            _stateField = type.GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic);
            _playerGraphicField = type.GetField("_playerGraphic", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        private static Color ColorFor(string skinId)
        {
            if (string.Equals(skinId, CosmeticIds.Neon, StringComparison.Ordinal))
                return new Color(0.20f, 1.00f, 0.90f, 1f);
            if (string.Equals(skinId, CosmeticIds.Retro, StringComparison.Ordinal))
                return new Color(1.00f, 0.35f, 0.75f, 1f);
            if (string.Equals(skinId, CosmeticIds.Gold, StringComparison.Ordinal))
                return new Color(1.00f, 0.88f, 0.28f, 1f);
            return new Color(1.00f, 0.75f, 0.15f, 1f);
        }
    }
}
