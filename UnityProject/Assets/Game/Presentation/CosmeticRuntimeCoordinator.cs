using System;
using DontGetSidetracked.Core;
using DontGetSidetracked.Economy;
using UnityEngine;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Applies the locally selected trail cosmetic only while a route is active.
    /// The Player graphic is empty during preview, and Result colors remain score-driven in GameBootstrap.
    /// </summary>
    public sealed class CosmeticRuntimeCoordinator : MonoBehaviour
    {
        private JsonFileSaveRepository _saveRepository;
        private GameBootstrap _bootstrap;
        private RouteGraphic _playerGraphic;
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
            ResolveRuntime();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + 0.2f;
            if (_bootstrap == null || _playerGraphic == null) ResolveRuntime();
            if (_bootstrap == null || _playerGraphic == null) return;
            if (!GameBootstrapRuntimeBridge.IsActiveRound(_bootstrap)) return;

            SaveData save = _saveRepository.Load();
            var selection = new CosmeticSelectionService(_saveRepository, save);
            string skin = selection.SelectedSkinId;
            Color desired = ColorFor(skin);
            if (!string.Equals(_lastSkin, skin, StringComparison.Ordinal) || _playerGraphic.color != desired)
            {
                _lastSkin = skin;
                _playerGraphic.color = desired;
            }
        }

        private void ResolveRuntime()
        {
            _bootstrap = FindFirstObjectByType<GameBootstrap>();
            _playerGraphic = null;

            GameObject gameCanvas = GameObject.Find("GameCanvas");
            if (gameCanvas == null) return;
            RouteGraphic[] graphics = gameCanvas.GetComponentsInChildren<RouteGraphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                if (!string.Equals(graphics[i].name, "Player", StringComparison.Ordinal)) continue;
                _playerGraphic = graphics[i];
                break;
            }
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
