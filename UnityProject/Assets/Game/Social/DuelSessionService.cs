using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DontGetSidetracked.Core;
using DontGetSidetracked.Gameplay;
using DontGetSidetracked.Services;

namespace DontGetSidetracked.Social
{
    public sealed class DuelSession
    {
        public ReferralDto Referral { get; }
        public DailyChallengeDefinition Challenge { get; }

        public DuelSession(ReferralDto referral, DailyChallengeDefinition challenge)
        {
            Referral = referral ?? throw new ArgumentNullException(nameof(referral));
            Challenge = challenge ?? throw new ArgumentNullException(nameof(challenge));
        }
    }

    public sealed class DuelSubmissionResult
    {
        public bool SubmittedToServer { get; }
        public double Score { get; }
        public double InviterScore { get; }
        public bool Won => Score > InviterScore;
        public bool Tied => Math.Abs(Score - InviterScore) < 0.05;

        public DuelSubmissionResult(bool submittedToServer, double score, double inviterScore)
        {
            SubmittedToServer = submittedToServer;
            Score = score;
            InviterScore = inviterScore;
        }
    }

    public sealed class DuelSessionService
    {
        private readonly IGameApi _api;
        private readonly ISaveRepository _saveRepository;
        private readonly SaveData _save;
        private readonly DailyChallengeFactory _factory = new DailyChallengeFactory();

        public SaveData Save => _save;

        public DuelSessionService(IGameApi api, ISaveRepository saveRepository, SaveData save)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
            _saveRepository = saveRepository ?? throw new ArgumentNullException(nameof(saveRepository));
            _save = SaveMigrator.Migrate(save ?? SaveData.CreateNew());
        }

        public async Task<DuelSession> LoadAsync(string referralId)
        {
            if (!ReferralLinkParser.IsValidReferralId(referralId))
                throw new ArgumentException("Referral id is invalid.", nameof(referralId));

            ReferralDto referral = await _api.GetReferralAsync(referralId);
            if (referral == null) throw new InvalidOperationException("Referral response is empty.");
            if (string.IsNullOrWhiteSpace(referral.ChallengeId))
                throw new InvalidOperationException("Referral challengeId is missing.");
            if (referral.GeneratorVersion <= 0)
                throw new InvalidOperationException("Referral generatorVersion is invalid.");

            DailyChallengeDefinition challenge = _factory.Create(
                referral.ChallengeId,
                referral.Seed,
                referral.GeneratorVersion);
            return new DuelSession(referral, challenge);
        }

        public async Task<DuelSubmissionResult> CompleteAsync(
            DuelSession session,
            IReadOnlyList<IReadOnlyList<RecordedPoint>> replays,
            IReadOnlyList<double> clientScores)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            ValidateAttempt(replays, clientScores);

            double localScore = DailyChallengeFactory.DailyScore(clientScores);
            if (localScore > _save.PersonalBest) _save.PersonalBest = localScore;

            bool submitted = false;
            double score = localScore;
            try
            {
                score = await _api.SubmitDailyAttemptAsync(
                    _save.AnonymousPlayerId,
                    session.Challenge,
                    replays,
                    clientScores,
                    false);
                submitted = true;
            }
            catch
            {
                _save.PendingAttempts.Add(ToPendingAttempt(session.Challenge, replays, clientScores));
            }

            if (string.Equals(_save.PendingReferralId, session.Referral.ReferralId, StringComparison.OrdinalIgnoreCase))
                _save.PendingReferralId = string.Empty;
            _saveRepository.Save(_save);

            return new DuelSubmissionResult(submitted, score, session.Referral.InviterScore);
        }

        private static PendingDailyAttemptData ToPendingAttempt(
            DailyChallengeDefinition challenge,
            IReadOnlyList<IReadOnlyList<RecordedPoint>> replays,
            IReadOnlyList<double> clientScores)
        {
            var pending = new PendingDailyAttemptData
            {
                ChallengeId = challenge.ChallengeId,
                Seed = challenge.Seed,
                GeneratorVersion = challenge.GeneratorVersion,
                Assisted = false
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

        private static void ValidateAttempt(
            IReadOnlyList<IReadOnlyList<RecordedPoint>> replays,
            IReadOnlyList<double> scores)
        {
            if (replays == null || replays.Count != 3)
                throw new ArgumentException("Duel requires three replays.", nameof(replays));
            if (scores == null || scores.Count != 3)
                throw new ArgumentException("Duel requires three scores.", nameof(scores));
            for (int i = 0; i < 3; i++)
                if (replays[i] == null || replays[i].Count == 0)
                    throw new ArgumentException("Duel replay cannot be empty.", nameof(replays));
        }
    }
}
