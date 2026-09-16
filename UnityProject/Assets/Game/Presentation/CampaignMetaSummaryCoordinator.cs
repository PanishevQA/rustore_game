using System;
using System.Reflection;
using DontGetSidetracked.Core;
using DontGetSidetracked.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Keeps the existing meta panel focused on presentation while enriching its Statistics view
    /// with local campaign progress. No network or platform SDK dependency.
    /// </summary>
    public sealed class CampaignMetaSummaryCoordinator : MonoBehaviour
    {
        private MetaMenuOverlay _meta;
        private FieldInfo _panelOpenField;
        private FieldInfo _titleField;
        private FieldInfo _bodyField;
        private JsonFileSaveRepository _repository;
        private float _nextRefresh;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (FindFirstObjectByType<CampaignMetaSummaryCoordinator>() != null) return;
            var root = new GameObject("CampaignMetaSummaryCoordinator");
            DontDestroyOnLoad(root);
            root.AddComponent<CampaignMetaSummaryCoordinator>();
        }

        private void Awake()
        {
            _repository = new JsonFileSaveRepository();
            Resolve();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 0.2f;
            if (_meta == null) Resolve();
            if (_meta == null) return;

            if (!(_panelOpenField?.GetValue(_meta) is bool open) || !open) return;
            Text title = _titleField?.GetValue(_meta) as Text;
            Text body = _bodyField?.GetValue(_meta) as Text;
            if (title == null || body == null || !string.Equals(title.text, "СТАТИСТИКА", StringComparison.Ordinal)) return;
            if (body.text.StartsWith("КАМПАНИЯ", StringComparison.Ordinal)) return;

            SaveData save = _repository.Load();
            var progress = new CampaignProgressService(_repository, save);
            string campaign =
                $"КАМПАНИЯ\n" +
                $"Пройдено уровней: {progress.CompletedLevels()}/{CampaignLevelCatalog.TotalLevels}\n" +
                $"Звёзды: {progress.TotalStars()}/{CampaignLevelCatalog.TotalLevels * 3}\n" +
                $"Открыт уровень: {progress.HighestUnlockedLevel}\n" +
                $"Монеты: {save.Coins}    Подсказки: {save.Hints}\n\n";
            body.text = campaign + body.text;
        }

        private void Resolve()
        {
            _meta = FindFirstObjectByType<MetaMenuOverlay>();
            if (_meta == null) return;
            BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Type type = typeof(MetaMenuOverlay);
            _panelOpenField = type.GetField("_panelOpen", flags);
            _titleField = type.GetField("_panelTitle", flags);
            _bodyField = type.GetField("_panelBody", flags);
        }
    }
}
