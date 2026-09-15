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

            try
            {
                double serverScore = await _api.SubmitDailyAttemptAsync(
                    _save.AnonymousPlayerId,
                    session.Challenge,
                    replays,
                    clientScores,
                    assisted);
                _saveRepository.Save(_save);
                return new DailyAttemptSubmissionResult(true, serverScore, localScore);
            }
            catch
            {
                _save.PendingAttempts.Add(ToPendingAttempt(session.Challenge, replays, clientScores, assisted));
                _saveRepository.Save(_save);
                return new DailyAttemptSubmissionResult(false, localScore, localScore);
            }
        }

        public async Task<int> FlushPendingAsync()
        {
            if (_save.PendingAttempts == null || _save.PendingAttempts.Count == 0) return 0;

            int flushed = 0;
            int index = 0;
            while (index < _save.PendingAttempts.Count)
            {
                PendingDailyAttemptData pending = _save.PendingAttempts[index];
                if (!TryRestorePending(pending, out DailyChallengeDefinition challenge, out List<IReadOnlyList<RecordedPoint>> replays, out List<double> scores))
                {
                    _save.PendingAttempts.RemoveAt(index);
                    _saveRepository.Save(_save);
                    continue;
                }

                try
                {
                    await _api.SubmitDailyAttemptAsync(
                        _save.AnonymousPlayerId,
                        challenge,
                        replays,
                        scores,
                        pending.Assisted);
                    _save.PendingAttempts.RemoveAt(index);
                    flushed++;
                    _saveRepository.Save(_save);
                }
                catch
                {
                    break;
                }
            }

            return flushed;
        }

        private static PendingDailyAttemptData ToPendingAttempt(
            DailyChallengeDefinition challenge,
            IReadOnlyList<IReadOnlyList<RecordedPoint>> replays,
            IReadOnlyList<double> clientScores,
            bool assisted)
        {
            var pending = new PendingDailyAttemptData
            {
                ChallengeId = challenge.ChallengeId,
                Seed = challenge.Seed,
                GeneratorVersion = challenge.GeneratorVersion,
                Assisted = assisted
            };

            for (int i = 0; i < 3; i++)
            {
                var route = new PendingRouteAttemptData
                {
                    RouteIndex = i,
                    ClientScore = clientScores[i]
                };
                IReadOnlyList<RecordedPoint> replay = replays[i];
                for (int p = 0; p < replay.Count; p++)
                {
                    route.Points.Add(new ReplayPointData
                    {
                        X = replay[p].Position.X,
                        Y = replay[p].Position.Y,
                        TimestampMs = replay[p].TimestampMs
                    });
                }
                pending.Routes.Add(route);
            }

            return pending;
        }

        private static bool TryRestorePending(
            PendingDailyAttemptData pending,
            out DailyChallengeDefinition challenge,
            out List<IReadOnlyList<RecordedPoint>> replays,
            out List<double> scores)
        {
            challenge = null;
            replays = null;
            scores = null;
            if (pending == null || string.IsNullOrWhiteSpace(pending.ChallengeId) || pending.GeneratorVersion <= 0)
                return false;
            if (pending.Routes == null || pending.Routes.Count != 3) return false;

            pending.Routes.Sort((a, b) => a.RouteIndex.CompareTo(b.RouteIndex));
            replays = new List<IReadOnlyList<RecordedPoint>>(3);
            scores = new List<double>(3);

            for (int i = 0; i < 3; i++)
            {
                PendingRouteAttemptData route = pending.Routes[i];
                if (route == null || route.RouteIndex != i || route.Points == null || route.Points.Count == 0) return false;
                var replay = new List<RecordedPoint>(route.Points.Count);
                for (int p = 0; p < route.Points.Count; p++)
                {
                    ReplayPointData point = route.Points[p];
                    replay.Add(new RecordedPoint(new FixedPoint2(point.X, point.Y), point.TimestampMs));
                }
                replays.Add(replay);
                scores.Add(route.ClientScore);
            }

            challenge = new DailyChallengeFactory().Create(pending.ChallengeId, pending.Seed, pending.GeneratorVersion);
            return true;
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
