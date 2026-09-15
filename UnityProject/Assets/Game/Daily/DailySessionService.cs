using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using DontGetSidetracked.Core;
using DontGetSidetracked.Gameplay;
using DontGetSidetracked.Services;

namespace DontGetSidetracked.Daily
{
    public sealed class DailyLoadResult
    {
        public DailyChallengeDefinition Challenge { get; }
        public DateTime ServerTimeUtc { get; }
        public bool FromCache { get; }

        public DailyLoadResult(DailyChallengeDefinition challenge, DateTime serverTimeUtc, bool fromCache)
        {
            Challenge = challenge ?? throw new ArgumentNullException(nameof(challenge));
            ServerTimeUtc = serverTimeUtc.Kind == DateTimeKind.Utc ? serverTimeUtc : serverTimeUtc.ToUniversalTime();
            FromCache = fromCache;
        }
    }

    public sealed class DailyAttemptSubmissionResult
    {
        // Name kept for presentation/API compatibility. In the offline-first release this means
        // that the configured IGameApi provider accepted the result; the provider is local.
        public bool SubmittedToServer { get; }
        public double ServerScore { get; }
        public double LocalScore { get; }

        public DailyAttemptSubmissionResult(bool submittedToServer, double serverScore, double localScore)
        {
            SubmittedToServer = submittedToServer;
            ServerScore = serverScore;
            LocalScore = localScore;
        }
    }

    public sealed class DailySessionService
    {
        private readonly IGameApi _api;
        private readonly ISaveRepository _saveRepository;
        private readonly DailyChallengeFactory _factory;
        private readonly StreakService _streakService;
        private readonly SaveData _save;

        public SaveData Save => _save;

        public DailySessionService(IGameApi api, ISaveRepository saveRepository, SaveData save)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
            _saveRepository = saveRepository ?? throw new ArgumentNullException(nameof(saveRepository));
            _save = SaveMigrator.Migrate(save ?? SaveData.CreateNew());
            _factory = new DailyChallengeFactory();
            _streakService = new StreakService();
        }

        public async Task<DailyLoadResult> LoadCurrentAsync()
        {
            try
            {
                DailyDto dto = await _api.GetDailyAsync();
                ValidateDaily(dto);
                DateTime serverTime = ParseServerTime(dto.ServerTimeUtc);
                DailyChallengeDefinition challenge = _factory.Create(dto.ChallengeId, dto.Seed, dto.GeneratorVersion);

                _save.LastDaily = new DailyCacheData
                {
                    ChallengeId = dto.ChallengeId,
                    Seed = dto.Seed,
                    GeneratorVersion = dto.GeneratorVersion,
                    ServerTimeUtc = serverTime.ToString("O", CultureInfo.InvariantCulture)
                };
                _saveRepository.Save(_save);
                return new DailyLoadResult(challenge, serverTime, false);
            }
            catch
            {
                DailyCacheData cache = _save.LastDaily;
                if (cache == null || string.IsNullOrWhiteSpace(cache.ChallengeId) || cache.GeneratorVersion <= 0)
                    throw;

                DateTime cachedServerTime = ParseServerTime(cache.ServerTimeUtc);
                DailyChallengeDefinition cached = _factory.Create(cache.ChallengeId, cache.Seed, cache.GeneratorVersion);
                return new DailyLoadResult(cached, cachedServerTime, true);
            }
        }

        public async Task<DailyAttemptSubmissionResult> CompleteAndSubmitAsync(
            DailyLoadResult session,
            IReadOnlyList<IReadOnlyList<RecordedPoint>> replays,
            IReadOnlyList<double> clientScores,
            bool assisted)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            ValidateAttempt(replays, clientScores);

            double localScore = DailyChallengeFactory.DailyScore(clientScores);
            if (localScore > _save.PersonalBest) _save.PersonalBest = localScore;
            _streakService.ApplyCompletedDaily(_save, session.ServerTimeUtc);

            // Persist the meaningful local result before invoking any provider. If the provider fails,
            // Presentation can report "saved locally" and there is no fake background-sync promise.
            _saveRepository.Save(_save);

            double acceptedScore = await _api.SubmitDailyAttemptAsync(
                _save.AnonymousPlayerId,
                session.Challenge,
                replays,
                clientScores,
                assisted);
            return new DailyAttemptSubmissionResult(true, acceptedScore, localScore);
        }

        public Task<int> FlushPendingAsync()
        {
            // Compatibility cleanup for pre-v8 saves only. The offline-first release never queues attempts.
            int removed = _save.PendingAttempts?.Count ?? 0;
            if (removed > 0)
            {
                _save.PendingAttempts.Clear();
                _saveRepository.Save(_save);
            }
            return Task.FromResult(removed);
        }

        private static void ValidateDaily(DailyDto dto)
        {
            if (dto == null) throw new InvalidOperationException("Daily response is empty.");
            if (string.IsNullOrWhiteSpace(dto.ChallengeId)) throw new InvalidOperationException("Daily challengeId is missing.");
            if (dto.GeneratorVersion <= 0) throw new InvalidOperationException("Daily generatorVersion is invalid.");
            ParseServerTime(dto.ServerTimeUtc);
        }

        private static void ValidateAttempt(IReadOnlyList<IReadOnlyList<RecordedPoint>> replays, IReadOnlyList<double> scores)
        {
            if (replays == null || replays.Count != 3) throw new ArgumentException("Daily requires three replays.", nameof(replays));
            if (scores == null || scores.Count != 3) throw new ArgumentException("Daily requires three scores.", nameof(scores));
            for (int i = 0; i < 3; i++)
                if (replays[i] == null || replays[i].Count == 0) throw new ArgumentException("Daily replay cannot be empty.", nameof(replays));
        }

        private static DateTime ParseServerTime(string value)
        {
            if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime parsed))
                throw new InvalidOperationException("Daily serverTimeUtc is invalid.");
            return parsed;
        }
    }
}
