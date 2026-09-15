using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using DontGetSidetracked.Core;
using DontGetSidetracked.Gameplay;
using DontGetSidetracked.Services;
using DontGetSidetracked.Social;
using UnityEngine;
using UnityEngine.Networking;

namespace DontGetSidetracked.Network
{
    /// <summary>
    /// Compatibility facade. With an empty base URL all gameplay/social operations stay on-device.
    /// A backend URL can still be supplied in a future build without changing gameplay code.
    /// </summary>
    public sealed class UnityGameApi : IGameApi, ILeaderboardApi, IPurchaseVerificationApi
    {
        private readonly string _baseUrl;
        private readonly int _timeoutSeconds;
        private readonly OfflineGameApi _offline;

        public bool IsOfflineOnly => _offline != null;

        public UnityGameApi(string baseUrl, int timeoutSeconds = 8)
        {
            _baseUrl = (baseUrl ?? string.Empty).TrimEnd('/');
            _timeoutSeconds = Math.Max(2, timeoutSeconds);
            if (string.IsNullOrWhiteSpace(_baseUrl)) _offline = new OfflineGameApi();
        }

        public async Task<DailyDto> GetDailyAsync()
        {
            if (_offline != null) return await _offline.GetDailyAsync();
            string json = await SendAsync("GET", "/daily", null);
            return JsonUtility.FromJson<DailyDto>(json);
        }

        public async Task<LeaderboardDto> GetDailyLeaderboardAsync(string challengeId, int limit = 100)
        {
            if (string.IsNullOrWhiteSpace(challengeId)) throw new ArgumentException("Challenge id is required.", nameof(challengeId));
            if (_offline != null)
            {
                return new LeaderboardDto
                {
                    challengeId = challengeId,
                    items = Array.Empty<LeaderboardItemDto>()
                };
            }

            limit = Math.Max(1, Math.Min(100, limit));
            string path = "/leaderboard/daily?challengeId=" + UnityWebRequest.EscapeURL(challengeId) + "&limit=" + limit;
            string json = await SendAsync("GET", path, null);
            return JsonUtility.FromJson<LeaderboardDto>(json);
        }

        public async Task<PurchaseVerificationResult> VerifyPurchaseAsync(
            string playerId,
            string productId,
            string invoiceId,
            string purchaseId)
        {
            if (string.IsNullOrWhiteSpace(playerId)) throw new ArgumentException("Player id is required.", nameof(playerId));
            if (string.IsNullOrWhiteSpace(productId)) throw new ArgumentException("Product id is required.", nameof(productId));
            if (string.IsNullOrWhiteSpace(invoiceId)) throw new ArgumentException("Invoice id is required.", nameof(invoiceId));

            if (_offline != null)
            {
                bool valid = !string.IsNullOrWhiteSpace(purchaseId);
                return new PurchaseVerificationResult(
                    valid,
                    productId,
                    purchaseId,
                    invoiceId,
                    valid ? "SDK_CONFIRMED_LOCAL" : "INVALID_LOCAL_PURCHASE",
                    valid ? string.Empty : "RuStore purchase result has no purchaseId.");
            }

            var payload = new PurchaseVerifyRequestDto
            {
                playerId = playerId,
                productId = productId,
                invoiceId = invoiceId,
                purchaseId = purchaseId
            };
            string json = await SendAsync("POST", "/purchase/verify", JsonUtility.ToJson(payload));
            PurchaseVerifyResponseDto response = JsonUtility.FromJson<PurchaseVerifyResponseDto>(json);
            if (response == null)
                return new PurchaseVerificationResult(false, productId, purchaseId, invoiceId, string.Empty, "Empty verification response.");

            return new PurchaseVerificationResult(
                response.verified,
                response.productId,
                response.purchaseId,
                response.invoiceId,
                response.status,
                response.errorMessage);
        }

        public async Task<double> SubmitDailyAttemptAsync(
            string playerId,
            DailyChallengeDefinition challenge,
            IReadOnlyList<IReadOnlyList<RecordedPoint>> replays,
            IReadOnlyList<double> clientScores,
            bool assisted)
        {
            if (_offline != null)
                return await _offline.SubmitDailyAttemptAsync(playerId, challenge, replays, clientScores, assisted);

            if (challenge == null) throw new ArgumentNullException(nameof(challenge));
            if (replays == null || replays.Count != 3) throw new ArgumentException("Daily requires three replays.", nameof(replays));
            if (clientScores == null || clientScores.Count != 3) throw new ArgumentException("Daily requires three scores.", nameof(clientScores));

            var request = new AttemptRequestDto
            {
                playerId = playerId,
                challengeId = challenge.ChallengeId,
                generatorVersion = challenge.GeneratorVersion,
                assisted = assisted,
                routes = new RouteAttemptRequestDto[3]
            };

            for (int i = 0; i < 3; i++)
            {
                IReadOnlyList<RecordedPoint> source = replays[i];
                var points = new ReplayPointDto[source.Count];
                for (int p = 0; p < source.Count; p++)
                {
                    points[p] = new ReplayPointDto
                    {
                        x = source[p].Position.X,
                        y = source[p].Position.Y,
                        timestampMs = source[p].TimestampMs
                    };
                }

                long duration = source.Count == 0 ? 1 : Math.Max(1, source[source.Count - 1].TimestampMs);
                request.routes[i] = new RouteAttemptRequestDto
                {
                    routeIndex = i,
                    durationMs = duration,
                    clientScore = clientScores[i],
                    points = points
                };
            }

            string json = await SendAsync("POST", "/attempt", JsonUtility.ToJson(request));
            return JsonUtility.FromJson<AttemptResponseDto>(json).score;
        }

        public async Task<string> CreateChallengeAsync(string playerId, string challengeId, double score)
        {
            if (_offline != null) return await _offline.CreateChallengeAsync(playerId, challengeId, score);

            var payload = new ChallengeRequestDto
            {
                inviterId = playerId,
                challengeId = challengeId,
                score = score
            };
            string json = await SendAsync("POST", "/challenge", JsonUtility.ToJson(payload));
            return JsonUtility.FromJson<ChallengeResponseDto>(json).shareUrl;
        }

        public async Task<ReferralDto> GetReferralAsync(string referralId)
        {
            if (_offline != null) return await _offline.GetReferralAsync(referralId);
            string encoded = UnityWebRequest.EscapeURL(referralId ?? string.Empty);
            string json = await SendAsync("GET", "/referral/" + encoded, null);
            return JsonUtility.FromJson<ReferralDto>(json);
        }

        private async Task<string> SendAsync(string method, string path, string json)
        {
            if (string.IsNullOrWhiteSpace(_baseUrl)) throw new InvalidOperationException("Remote backend is disabled for this build.");

            using var request = new UnityWebRequest(_baseUrl + path, method);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = _timeoutSeconds;
            request.SetRequestHeader("Accept", "application/json");

            if (json != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.SetRequestHeader("Content-Type", "application/json");
            }

            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            var completion = new TaskCompletionSource<bool>();
            operation.completed += _ => completion.TrySetResult(true);
            await completion.Task;

            if (request.result != UnityWebRequest.Result.Success)
            {
                string body = request.downloadHandler?.text ?? string.Empty;
                throw new InvalidOperationException($"HTTP {(long)request.responseCode}: {request.error} {body}".Trim());
            }

            return request.downloadHandler.text;
        }

        [Serializable]
        private sealed class ReplayPointDto { public int x; public int y; public long timestampMs; }

        [Serializable]
        private sealed class RouteAttemptRequestDto
        {
            public int routeIndex;
            public long durationMs;
            public double clientScore;
            public ReplayPointDto[] points;
        }

        [Serializable]
        private sealed class AttemptRequestDto
        {
            public string playerId;
            public string challengeId;
            public int generatorVersion;
            public bool assisted;
            public RouteAttemptRequestDto[] routes;
        }

        [Serializable]
        private sealed class AttemptResponseDto { public double score; }

        [Serializable]
        private sealed class ChallengeRequestDto
        {
            public string inviterId;
            public string challengeId;
            public double score;
        }

        [Serializable]
        private sealed class ChallengeResponseDto { public string shareUrl; }

        [Serializable]
        private sealed class PurchaseVerifyRequestDto
        {
            public string playerId;
            public string productId;
            public string invoiceId;
            public string purchaseId;
        }

        [Serializable]
        private sealed class PurchaseVerifyResponseDto
        {
            public bool verified;
            public string productId;
            public string purchaseId;
            public string invoiceId;
            public string status;
            public string errorMessage;
        }
    }
}
