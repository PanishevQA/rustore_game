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
        // Kept for presentation/API compatibility. In the release build the accepted provider is local.
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
            if (referral.RouteCount < 1 || referral.RouteCount > 3)
                throw new InvalidOperationException("Referral routeCount must be between 1 and 3.");

            DailyChallengeDefinition challenge = _factory.Create(
                referral.ChallengeId,
                referral.Seed,
                referral.GeneratorVersion,
                referral.RouteCount);
            return new DuelSession(referral, challenge);
        }

        public async Task<DuelSubmissionResult> CompleteAsync(
            DuelSession session,
            IReadOnlyList<IReadOnlyList<RecordedPoint>> replays,
            IReadOnlyList<double> clientScores)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            ValidateAttempt(session.Challenge, replays, clientScores);

            double localScore = DailyChallengeFactory.DailyScore(clientScores);
            if (localScore > _save.PersonalBest) _save.PersonalBest = localScore;
            if (string.Equals(_save.PendingReferralId, session.Referral.ReferralId, StringComparison.OrdinalIgnoreCase))
                _save.PendingReferralId = string.Empty;

            // Persist local progress before invoking the provider; a provider failure never creates a
            // nonexistent "sync later" queue in the offline-first release.
            _saveRepository.Save(_save);

            double acceptedScore = await _api.SubmitDailyAttemptAsync(
                _save.AnonymousPlayerId,
                session.Challenge,
                replays,
                clientScores,
                false);

            return new DuelSubmissionResult(true, acceptedScore, session.Referral.InviterScore);
        }

        private static void ValidateAttempt(
            DailyChallengeDefinition challenge,
            IReadOnlyList<IReadOnlyList<RecordedPoint>> replays,
            IReadOnlyList<double> scores)
        {
            int expected = challenge?.RouteCount ?? 0;
            if (expected < 1) throw new ArgumentException("Duel challenge route count is invalid.", nameof(challenge));
            if (replays == null || replays.Count != expected)
                throw new ArgumentException($"Duel requires {expected} replay(s).", nameof(replays));
            if (scores == null || scores.Count != expected)
                throw new ArgumentException($"Duel requires {expected} score(s).", nameof(scores));
            for (int i = 0; i < expected; i++)
                if (replays[i] == null || replays[i].Count == 0)
                    throw new ArgumentException("Duel replay cannot be empty.", nameof(replays));
        }
    }
}
