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
        // Names kept for compatibility with the optional online provider. In offline-first runtime
        // SubmittedToServer simply means the configured IGameApi accepted/recalculated the result locally.
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
                int routeCount = NormalizeRouteCount(dto.RouteCount);
                DailyChallengeDefinition challenge = _factory.Create(
                    dto.ChallengeId,
                    dto.Seed,
                    dto.GeneratorVersion,
                    routeCount);

                _save.LastDaily = new DailyCacheData
                {
                    ChallengeId = dto.ChallengeId,
                    Seed = dto.Seed,
                    GeneratorVersion = dto.GeneratorVersion,
                    RouteCount = routeCount,
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
                DailyChallengeDefinition cached = _factory.Create(
                    cache.ChallengeId,
                    cache.Seed,
                    cache.GeneratorVersion,
                    NormalizeRouteCount(cache.RouteCount));
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
            ValidateAttempt(session.Challenge, replays, clientScores);

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
            if (dto.RouteCount < 1 || dto.RouteCount > 3) throw new InvalidOperationException("Daily routeCount must be between 1 and 3.");
            ParseServerTime(dto.ServerTimeUtc);
        }

        private static void ValidateAttempt(
            DailyChallengeDefinition challenge,
            IReadOnlyList<IReadOnlyList<RecordedPoint>> replays,
            IReadOnlyList<double> scores)
        {
            int expected = challenge?.RouteCount ?? 0;
            if (expected < 1) throw new ArgumentException("Daily challenge route count is invalid.", nameof(challenge));
            if (replays == null || replays.Count != expected)
                throw new ArgumentException($"Daily requires {expected} replay(s).", nameof(replays));
            if (scores == null || scores.Count != expected)
                throw new ArgumentException($"Daily requires {expected} score(s).", nameof(scores));
            for (int i = 0; i < expected; i++)
                if (replays[i] == null || replays[i].Count == 0)
                    throw new ArgumentException("Daily replay cannot be empty.", nameof(replays));
        }

        private static int NormalizeRouteCount(int value) => value >= 1 && value <= 3 ? value : 3;

        private static DateTime ParseServerTime(string value)
        {
            if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime parsed))
                throw new InvalidOperationException("Daily serverTimeUtc is invalid.");
            return parsed;
        }
    }
}
