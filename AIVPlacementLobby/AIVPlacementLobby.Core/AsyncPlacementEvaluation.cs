using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AIVParser.Core;
using AIVPlacement.Core;
using MapParser.Core;

namespace AIVPlacementLobby.Core
{
    public enum LobbyEvaluationFailureKind
    {
        None,
        RequestNotReady,
        MapReadFailed,
        MapChangedDuringRead,
        MapSnapshotUnavailable,
        KeepAnchorUnavailable,
        AivSourceUnavailable,
        AivChangedDuringRead,
        AivParseFailed,
        PlacementEvaluationFailed
    }

    public enum LobbyEvaluationCacheDisposition
    {
        Computed,
        ResultCacheHit,
        SharedInFlight
    }

    public sealed class LobbyPlacementPhaseTimings
    {
        public LobbyPlacementPhaseTimings(
            TimeSpan mapParse,
            TimeSpan snapshot,
            TimeSpan aivParse,
            TimeSpan projection,
            TimeSpan ruleEvaluation,
            bool mapCacheHit,
            bool mapLoadShared)
        {
            MapParse = mapParse;
            Snapshot = snapshot;
            AivParse = aivParse;
            Projection = projection;
            RuleEvaluation = ruleEvaluation;
            MapCacheHit = mapCacheHit;
            MapLoadShared = mapLoadShared;
        }

        public static LobbyPlacementPhaseTimings Empty { get; } =
            new LobbyPlacementPhaseTimings(
                TimeSpan.Zero,
                TimeSpan.Zero,
                TimeSpan.Zero,
                TimeSpan.Zero,
                TimeSpan.Zero,
                false,
                false);

        public TimeSpan MapParse { get; }
        public TimeSpan Snapshot { get; }
        public TimeSpan AivParse { get; }
        public TimeSpan Projection { get; }
        public TimeSpan RuleEvaluation { get; }
        public bool MapCacheHit { get; }
        public bool MapLoadShared { get; }
    }

    public sealed class LobbyPlacementWorkerResult
    {
        public LobbyPlacementWorkerResult(
            AivPlacementRotationSelection selection,
            LobbyEvaluationFailureKind failureKind,
            string failureMessage,
            LobbyPlacementPhaseTimings timings)
        {
            Selection = selection;
            FailureKind = failureKind;
            FailureMessage = failureMessage ?? string.Empty;
            Timings = timings ?? LobbyPlacementPhaseTimings.Empty;
        }

        public AivPlacementRotationSelection Selection { get; }
        public LobbyEvaluationFailureKind FailureKind { get; }
        public string FailureMessage { get; }
        public LobbyPlacementPhaseTimings Timings { get; }
        public bool IsEvaluable => FailureKind == LobbyEvaluationFailureKind.None && Selection != null;

        public static LobbyPlacementWorkerResult NotEvaluable(
            LobbyEvaluationFailureKind failureKind,
            string message) =>
            new LobbyPlacementWorkerResult(
                null,
                failureKind,
                message,
                LobbyPlacementPhaseTimings.Empty);
    }

    public sealed class AivPlacementCandidateWorkItem
    {
        internal AivPlacementCandidateWorkItem(
            AivPlacementCheckRequest request,
            AivPlacementCandidateRequest candidate,
            LobbyFileStamp mapStamp,
            LobbyAivSourceStamp aivStamp,
            string assetText)
        {
            Request = request;
            Candidate = candidate;
            MapStamp = mapStamp;
            AivStamp = aivStamp;
            AssetText = assetText;
        }

        public AivPlacementCheckRequest Request { get; }
        public AivPlacementCandidateRequest Candidate { get; }
        public LobbyFileStamp MapStamp { get; }
        public LobbyAivSourceStamp AivStamp { get; }
        public string AssetText { get; }
    }

    public interface ILobbyPlacementCandidateWorker
    {
        LobbyPlacementWorkerResult Evaluate(AivPlacementCandidateWorkItem workItem);
    }

    public sealed class AivPlacementCandidateEvaluation
    {
        internal AivPlacementCandidateEvaluation(
            AivPlacementCandidateRequest candidate,
            LobbyPlacementWorkerResult workerResult,
            LobbyEvaluationCacheDisposition cacheDisposition)
        {
            CandidateId = candidate.CandidateId;
            CandidateName = candidate.Name;
            Source = candidate.Source;
            Selection = workerResult.Selection;
            FailureKind = workerResult.FailureKind;
            FailureMessage = workerResult.FailureMessage;
            CacheDisposition = cacheDisposition;
            Timings = cacheDisposition == LobbyEvaluationCacheDisposition.Computed
                ? workerResult.Timings
                : LobbyPlacementPhaseTimings.Empty;
        }

        public int CandidateId { get; }
        public string CandidateName { get; }
        public string Source { get; }
        public AivPlacementRotationSelection Selection { get; }
        public LobbyEvaluationFailureKind FailureKind { get; }
        public string FailureMessage { get; }
        public LobbyEvaluationCacheDisposition CacheDisposition { get; }
        public LobbyPlacementPhaseTimings Timings { get; }
        public AivPlacementStatus Status => Selection == null
            ? AivPlacementStatus.NotEvaluable
            : Selection.Status;
    }

    public sealed class AivPlacementCheckResult
    {
        internal AivPlacementCheckResult(
            AivPlacementCheckRequest request,
            AivPlacementStatus status,
            AivPlacementCandidateEvaluation selectedCandidate,
            AivPlacementResult selectedVariant,
            IEnumerable<AivPlacementCandidateEvaluation> candidates,
            LobbyEvaluationFailureKind failureKind,
            string failureMessage,
            TimeSpan elapsed)
        {
            Generation = request.Generation;
            PlayerId = request.PlayerId;
            KeepSlotIndex = request.KeepSlotIndex;
            PreBuildSetting = request.PreBuildSetting;
            Status = status;
            SelectedCandidate = selectedCandidate;
            SelectedVariant = selectedVariant;
            Candidates = new ReadOnlyCollection<AivPlacementCandidateEvaluation>(
                new List<AivPlacementCandidateEvaluation>(
                    candidates ?? Array.Empty<AivPlacementCandidateEvaluation>()));
            FailureKind = failureKind;
            FailureMessage = failureMessage ?? string.Empty;
            Elapsed = elapsed;
        }

        public long Generation { get; }
        public int PlayerId { get; }
        public int KeepSlotIndex { get; }
        public int PreBuildSetting { get; }
        public AivPlacementStatus Status { get; }
        public AivPlacementCandidateEvaluation SelectedCandidate { get; }
        public AivPlacementResult SelectedVariant { get; }
        public IReadOnlyList<AivPlacementCandidateEvaluation> Candidates { get; }
        public LobbyEvaluationFailureKind FailureKind { get; }
        public string FailureMessage { get; }
        public TimeSpan Elapsed { get; }
    }

    public readonly struct LobbyFileStamp : IEquatable<LobbyFileStamp>
    {
        private LobbyFileStamp(string path, bool exists, long length, long lastWriteUtcTicks)
        {
            Path = path;
            Exists = exists;
            Length = length;
            LastWriteUtcTicks = lastWriteUtcTicks;
        }

        public string Path { get; }
        public bool Exists { get; }
        public long Length { get; }
        public long LastWriteUtcTicks { get; }

        public static LobbyFileStamp Capture(string path)
        {
            string normalized = NormalizePath(path);
            try
            {
                var info = new FileInfo(normalized);
                return info.Exists
                    ? new LobbyFileStamp(normalized, true, info.Length, info.LastWriteTimeUtc.Ticks)
                    : new LobbyFileStamp(normalized, false, 0, 0);
            }
            catch
            {
                return new LobbyFileStamp(normalized, false, 0, 0);
            }
        }

        public bool Equals(LobbyFileStamp other) =>
            StringComparer.OrdinalIgnoreCase.Equals(Path, other.Path) &&
            Exists == other.Exists &&
            Length == other.Length &&
            LastWriteUtcTicks == other.LastWriteUtcTicks;

        public override bool Equals(object obj) => obj is LobbyFileStamp other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.OrdinalIgnoreCase.GetHashCode(Path ?? string.Empty);
                hash = hash * 397 ^ Exists.GetHashCode();
                hash = hash * 397 ^ Length.GetHashCode();
                return hash * 397 ^ LastWriteUtcTicks.GetHashCode();
            }
        }

        public override string ToString() =>
            $"{Path}|{(Exists ? 1 : 0)}|{Length}|{LastWriteUtcTicks}";

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;
            try
            {
                return System.IO.Path.GetFullPath(path);
            }
            catch
            {
                return path;
            }
        }
    }

    public readonly struct LobbyAivSourceStamp : IEquatable<LobbyAivSourceStamp>
    {
        internal LobbyAivSourceStamp(
            LobbyCandidateSourceKind sourceKind,
            string source,
            ulong checksum,
            LobbyFileStamp fileStamp,
            string contentHash)
        {
            SourceKind = sourceKind;
            Source = source ?? string.Empty;
            Checksum = checksum;
            FileStamp = fileStamp;
            ContentHash = contentHash ?? string.Empty;
        }

        public LobbyCandidateSourceKind SourceKind { get; }
        public string Source { get; }
        public ulong Checksum { get; }
        public LobbyFileStamp FileStamp { get; }
        public string ContentHash { get; }

        public bool Equals(LobbyAivSourceStamp other) =>
            SourceKind == other.SourceKind &&
            StringComparer.OrdinalIgnoreCase.Equals(Source, other.Source) &&
            Checksum == other.Checksum &&
            FileStamp.Equals(other.FileStamp) &&
            string.Equals(ContentHash, other.ContentHash, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is LobbyAivSourceStamp other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)SourceKind;
                hash = hash * 397 ^ StringComparer.OrdinalIgnoreCase.GetHashCode(Source);
                hash = hash * 397 ^ Checksum.GetHashCode();
                hash = hash * 397 ^ FileStamp.GetHashCode();
                return hash * 397 ^ StringComparer.Ordinal.GetHashCode(ContentHash);
            }
        }
    }

    public sealed class AivPlacementEvaluationService
    {
        public const string AnalyzerVersion = "chat12-noprebuild-v1";

        private readonly object sync = new object();
        private readonly ILobbyPlacementCandidateWorker worker;
        private readonly SemaphoreSlim concurrency;
        private readonly BoundedLruCache<AivPlacementEvaluationKey, LobbyPlacementWorkerResult> resultCache;
        private readonly Dictionary<AivPlacementEvaluationKey, Task<LobbyPlacementWorkerResult>> inFlight =
            new Dictionary<AivPlacementEvaluationKey, Task<LobbyPlacementWorkerResult>>();

        public AivPlacementEvaluationService(
            int maximumResultEntries = 256,
            int maximumMapEntries = 4,
            int maximumConcurrency = 2)
            : this(
                new OfflineLobbyPlacementCandidateWorker(maximumMapEntries),
                maximumResultEntries,
                maximumConcurrency)
        {
        }

        public AivPlacementEvaluationService(
            ILobbyPlacementCandidateWorker worker,
            int maximumResultEntries = 256,
            int maximumConcurrency = 2)
        {
            this.worker = worker ?? throw new ArgumentNullException(nameof(worker));
            if (maximumResultEntries < 1)
                throw new ArgumentOutOfRangeException(nameof(maximumResultEntries));
            if (maximumConcurrency < 1)
                throw new ArgumentOutOfRangeException(nameof(maximumConcurrency));

            resultCache = new BoundedLruCache<AivPlacementEvaluationKey, LobbyPlacementWorkerResult>(
                maximumResultEntries);
            concurrency = new SemaphoreSlim(maximumConcurrency, maximumConcurrency);
        }

        public Task<AivPlacementCheckResult> EvaluateAsync(
            AivPlacementCheckRequest request,
            IReadOnlyDictionary<string, string> scriptExtenderAssets = null)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var assets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (scriptExtenderAssets != null)
            {
                foreach (KeyValuePair<string, string> pair in scriptExtenderAssets)
                    assets[pair.Key] = pair.Value;
            }

            // File metadata and every parse/evaluation phase stay off Unity's main thread.
            return Task.Run(() => EvaluateRequestCoreAsync(request, assets));
        }

        public static string BuildSourceFingerprint(
            AivPlacementRequestBatch batch,
            IReadOnlyDictionary<string, string> scriptExtenderAssets = null)
        {
            if (batch == null)
                throw new ArgumentNullException(nameof(batch));

            var text = new StringBuilder();
            foreach (AivPlacementCheckRequest request in batch.Requests)
            {
                text.Append(LobbyFileStamp.Capture(request.MapPath)).Append('|')
                    .Append(request.PreBuildSetting).Append('|')
                    .Append(request.KeepSlotIndex).Append('|')
                    .Append((int)request.InitialRotation).Append('|');
                foreach (AivPlacementCandidateRequest candidate in request.Candidates)
                {
                    text.Append((int)candidate.SourceKind).Append('|')
                        .Append(candidate.Source).Append('|')
                        .Append(candidate.Checksum).Append('|');
                    if (candidate.SourceKind == LobbyCandidateSourceKind.File)
                    {
                        text.Append(LobbyFileStamp.Capture(candidate.Source));
                    }
                    else
                    {
                        string content = null;
                        scriptExtenderAssets?.TryGetValue(candidate.Source, out content);
                        text.Append(HashText(content));
                    }
                    text.Append('|');
                }
            }
            return HashText(text.ToString());
        }

        private async Task<AivPlacementCheckResult> EvaluateRequestCoreAsync(
            AivPlacementCheckRequest request,
            IReadOnlyDictionary<string, string> assets)
        {
            var elapsed = Stopwatch.StartNew();
            if (!request.IsReady)
            {
                return new AivPlacementCheckResult(
                    request,
                    AivPlacementStatus.NotEvaluable,
                    null,
                    null,
                    Array.Empty<AivPlacementCandidateEvaluation>(),
                    LobbyEvaluationFailureKind.RequestNotReady,
                    request.FailureKind.ToString(),
                    elapsed.Elapsed);
            }

            LobbyFileStamp mapStamp = LobbyFileStamp.Capture(request.MapPath);
            var pending = new List<Task<CandidateFetch>>(request.Candidates.Count);
            foreach (AivPlacementCandidateRequest candidate in request.Candidates)
            {
                string assetText = null;
                if (candidate.SourceKind == LobbyCandidateSourceKind.ScriptExtenderAsset)
                    assets.TryGetValue(candidate.Source, out assetText);

                LobbyAivSourceStamp aivStamp = CreateAivStamp(candidate, assetText);
                var key = new AivPlacementEvaluationKey(
                    mapStamp,
                    aivStamp,
                    request.KeepSlotIndex,
                    request.InitialRotation,
                    request.PreBuildSetting,
                    AnalyzerVersion);
                var item = new AivPlacementCandidateWorkItem(
                    request,
                    candidate,
                    mapStamp,
                    aivStamp,
                    assetText);
                pending.Add(GetOrEvaluateAsync(key, item));
            }

            CandidateFetch[] fetched = await Task.WhenAll(pending).ConfigureAwait(false);
            var candidates = new List<AivPlacementCandidateEvaluation>(fetched.Length);
            for (int index = 0; index < fetched.Length; index++)
            {
                candidates.Add(new AivPlacementCandidateEvaluation(
                    request.Candidates[index],
                    fetched[index].Result,
                    fetched[index].Disposition));
            }

            elapsed.Stop();
            return Aggregate(request, candidates, elapsed.Elapsed);
        }

        private Task<CandidateFetch> GetOrEvaluateAsync(
            AivPlacementEvaluationKey key,
            AivPlacementCandidateWorkItem item)
        {
            Task<LobbyPlacementWorkerResult> task;
            bool owner = false;
            lock (sync)
            {
                if (resultCache.TryGetValue(key, out LobbyPlacementWorkerResult cached))
                {
                    return Task.FromResult(new CandidateFetch(
                        cached,
                        LobbyEvaluationCacheDisposition.ResultCacheHit));
                }

                if (!inFlight.TryGetValue(key, out task))
                {
                    task = RunWorkerAsync(item);
                    inFlight.Add(key, task);
                    owner = true;
                }
            }

            return FinishCandidateAsync(key, task, owner);
        }

        private async Task<LobbyPlacementWorkerResult> RunWorkerAsync(
            AivPlacementCandidateWorkItem item)
        {
            await concurrency.WaitAsync().ConfigureAwait(false);
            try
            {
                return await Task.Run(() => worker.Evaluate(item)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return LobbyPlacementWorkerResult.NotEvaluable(
                    LobbyEvaluationFailureKind.PlacementEvaluationFailed,
                    ex.ToString());
            }
            finally
            {
                concurrency.Release();
            }
        }

        private async Task<CandidateFetch> FinishCandidateAsync(
            AivPlacementEvaluationKey key,
            Task<LobbyPlacementWorkerResult> task,
            bool owner)
        {
            LobbyPlacementWorkerResult result = await task.ConfigureAwait(false);
            lock (sync)
            {
                if (owner)
                {
                    inFlight.Remove(key);
                    // Proven failures are cached too; a changed file creates a different key.
                    resultCache.Set(key, result);
                }
            }

            return new CandidateFetch(
                result,
                owner
                    ? LobbyEvaluationCacheDisposition.Computed
                    : LobbyEvaluationCacheDisposition.SharedInFlight);
        }

        private static AivPlacementCheckResult Aggregate(
            AivPlacementCheckRequest request,
            IReadOnlyList<AivPlacementCandidateEvaluation> candidates,
            TimeSpan elapsed)
        {
            if (candidates.Count == 0)
            {
                return NotEvaluable(
                    request,
                    candidates,
                    LobbyEvaluationFailureKind.AivSourceUnavailable,
                    "No AIV candidates were available.",
                    elapsed);
            }

            CandidateChoice firstAbove95 = null;
            CandidateChoice bestSequential = null;
            CandidateChoice bestPercentage = null;
            for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
            {
                AivPlacementCandidateEvaluation candidate = candidates[candidateIndex];
                if (!TryGetVariant(candidate, 0, out AivPlacementResult variant, out string error))
                {
                    return NotEvaluable(
                        request,
                        candidates,
                        candidate.FailureKind == LobbyEvaluationFailureKind.None
                            ? LobbyEvaluationFailureKind.PlacementEvaluationFailed
                            : candidate.FailureKind,
                        error,
                        elapsed);
                }

                if (variant.Status == AivPlacementStatus.Complete)
                    return Selected(request, candidates, candidate, variant, AivPlacementStatus.Complete, elapsed);

                var choice = new CandidateChoice(candidate, variant);
                if (firstAbove95 == null && variant.Score.FitPercentage > 95)
                    firstAbove95 = choice;
                if (bestSequential == null ||
                    variant.Score.SequentialBuildScore > bestSequential.Variant.Score.SequentialBuildScore)
                {
                    bestSequential = choice;
                }
                if (bestPercentage == null ||
                    variant.Score.FitPercentage > bestPercentage.Variant.Score.FitPercentage)
                {
                    bestPercentage = choice;
                }
            }

            if (bestSequential != null && bestSequential.Variant.Score.SequentialBuildScore > 0)
            {
                CandidateChoice selected = firstAbove95 ??
                    (bestSequential.Variant.Score.SequentialBuildScore >= 30
                        ? bestSequential
                        : bestPercentage.Variant.Score.FitPercentage > 90
                            ? bestPercentage
                            : bestSequential);
                return Selected(
                    request,
                    candidates,
                    selected.Candidate,
                    selected.Variant,
                    AivPlacementStatus.Partial,
                    elapsed);
            }

            CandidateChoice bestRotated = null;
            for (int rotationIndex = 1; rotationIndex < 4; rotationIndex++)
            {
                for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
                {
                    AivPlacementCandidateEvaluation candidate = candidates[candidateIndex];
                    if (!TryGetVariant(candidate, rotationIndex, out AivPlacementResult variant, out string error))
                    {
                        return NotEvaluable(
                            request,
                            candidates,
                            candidate.FailureKind == LobbyEvaluationFailureKind.None
                                ? LobbyEvaluationFailureKind.PlacementEvaluationFailed
                                : candidate.FailureKind,
                            error,
                            elapsed);
                    }
                    if (variant.Status == AivPlacementStatus.Complete)
                        return Selected(request, candidates, candidate, variant, AivPlacementStatus.Complete, elapsed);
                    if (variant.Status != AivPlacementStatus.Partial ||
                        variant.Score.FitPercentage <= 85)
                    {
                        continue;
                    }

                    if (bestRotated == null ||
                        variant.Score.FitPercentage > bestRotated.Variant.Score.FitPercentage)
                    {
                        // Strict comparison preserves native rotation/candidate order on ties.
                        bestRotated = new CandidateChoice(candidate, variant);
                    }
                }
            }

            return bestRotated == null
                ? new AivPlacementCheckResult(
                    request,
                    AivPlacementStatus.Impossible,
                    null,
                    null,
                    candidates,
                    LobbyEvaluationFailureKind.None,
                    string.Empty,
                    elapsed)
                : Selected(
                    request,
                    candidates,
                    bestRotated.Candidate,
                    bestRotated.Variant,
                    AivPlacementStatus.Partial,
                    elapsed);
        }

        private static bool TryGetVariant(
            AivPlacementCandidateEvaluation candidate,
            int rotationIndex,
            out AivPlacementResult variant,
            out string error)
        {
            variant = null;
            if (candidate.Selection == null)
            {
                error = string.IsNullOrEmpty(candidate.FailureMessage)
                    ? $"Candidate {candidate.CandidateId} is not evaluable."
                    : candidate.FailureMessage;
                return false;
            }
            if (rotationIndex < 0 || rotationIndex >= candidate.Selection.Variants.Count)
            {
                error = $"Candidate {candidate.CandidateId} has no rotation result {rotationIndex}.";
                return false;
            }

            variant = candidate.Selection.Variants[rotationIndex];
            if (variant.Status == AivPlacementStatus.NotEvaluable)
            {
                error = $"Candidate {candidate.CandidateId}, rotation {(int)variant.Rotation} is not evaluable.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static AivPlacementCheckResult Selected(
            AivPlacementCheckRequest request,
            IReadOnlyList<AivPlacementCandidateEvaluation> candidates,
            AivPlacementCandidateEvaluation candidate,
            AivPlacementResult variant,
            AivPlacementStatus status,
            TimeSpan elapsed) =>
            new AivPlacementCheckResult(
                request,
                status,
                candidate,
                variant,
                candidates,
                LobbyEvaluationFailureKind.None,
                string.Empty,
                elapsed);

        private static AivPlacementCheckResult NotEvaluable(
            AivPlacementCheckRequest request,
            IReadOnlyList<AivPlacementCandidateEvaluation> candidates,
            LobbyEvaluationFailureKind failureKind,
            string message,
            TimeSpan elapsed) =>
            new AivPlacementCheckResult(
                request,
                AivPlacementStatus.NotEvaluable,
                null,
                null,
                candidates,
                failureKind,
                message,
                elapsed);

        private static LobbyAivSourceStamp CreateAivStamp(
            AivPlacementCandidateRequest candidate,
            string assetText) =>
            candidate.SourceKind == LobbyCandidateSourceKind.File
                ? new LobbyAivSourceStamp(
                    candidate.SourceKind,
                    candidate.Source,
                    candidate.Checksum,
                    LobbyFileStamp.Capture(candidate.Source),
                    string.Empty)
                : new LobbyAivSourceStamp(
                    candidate.SourceKind,
                    candidate.Source,
                    candidate.Checksum,
                    default,
                    HashText(assetText));

        private static string HashText(string value)
        {
            if (value == null)
                return "<missing>";
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
                var result = new StringBuilder(hash.Length * 2);
                foreach (byte item in hash)
                    result.Append(item.ToString("x2"));
                return result.ToString();
            }
        }

        private sealed class CandidateChoice
        {
            public CandidateChoice(
                AivPlacementCandidateEvaluation candidate,
                AivPlacementResult variant)
            {
                Candidate = candidate;
                Variant = variant;
            }

            public AivPlacementCandidateEvaluation Candidate { get; }
            public AivPlacementResult Variant { get; }
        }

        private readonly struct CandidateFetch
        {
            public CandidateFetch(
                LobbyPlacementWorkerResult result,
                LobbyEvaluationCacheDisposition disposition)
            {
                Result = result;
                Disposition = disposition;
            }

            public LobbyPlacementWorkerResult Result { get; }
            public LobbyEvaluationCacheDisposition Disposition { get; }
        }
    }

    public sealed class OfflineLobbyPlacementCandidateWorker : ILobbyPlacementCandidateWorker
    {
        private readonly object mapSync = new object();
        private readonly BoundedLruCache<LobbyFileStamp, PreparedMap> mapCache;
        private readonly Dictionary<LobbyFileStamp, Lazy<PreparedMap>> mapInFlight =
            new Dictionary<LobbyFileStamp, Lazy<PreparedMap>>();
        private readonly AivCastleProjector projector = new AivCastleProjector();
        private readonly AivPlacementEvaluator evaluator = new AivPlacementEvaluator();

        public OfflineLobbyPlacementCandidateWorker(int maximumMapEntries = 4)
        {
            if (maximumMapEntries < 1)
                throw new ArgumentOutOfRangeException(nameof(maximumMapEntries));
            mapCache = new BoundedLruCache<LobbyFileStamp, PreparedMap>(maximumMapEntries);
        }

        public LobbyPlacementWorkerResult Evaluate(AivPlacementCandidateWorkItem workItem)
        {
            if (workItem == null)
                throw new ArgumentNullException(nameof(workItem));

            PreparedMapLookup mapLookup = GetPreparedMap(workItem.MapStamp);
            if (!mapLookup.Map.IsReady)
            {
                return Failure(
                    mapLookup.Map.FailureKind,
                    mapLookup.Map.FailureMessage,
                    mapLookup,
                    TimeSpan.Zero,
                    TimeSpan.Zero,
                    TimeSpan.Zero);
            }

            MapKeepAnchorResult keep = mapLookup.Map.Anchors.GetSlot(workItem.Request.KeepSlotIndex);
            if (keep.Status != MapKeepAnchorStatus.Exact || !keep.Coordinate.HasValue)
            {
                return Failure(
                    LobbyEvaluationFailureKind.KeepAnchorUnavailable,
                    keep.FailureKind.ToString(),
                    mapLookup,
                    TimeSpan.Zero,
                    TimeSpan.Zero,
                    TimeSpan.Zero);
            }

            var aivTimer = Stopwatch.StartNew();
            AivJsonLoadResult loaded;
            if (workItem.Candidate.SourceKind == LobbyCandidateSourceKind.File)
            {
                if (!workItem.AivStamp.FileStamp.Exists)
                {
                    return Failure(
                        LobbyEvaluationFailureKind.AivSourceUnavailable,
                        $"AIV file is unavailable: {workItem.Candidate.Source}",
                        mapLookup,
                        aivTimer.Elapsed,
                        TimeSpan.Zero,
                        TimeSpan.Zero);
                }
                loaded = AivJsonFileLoader.Load(workItem.Candidate.Source);
                if (!LobbyFileStamp.Capture(workItem.Candidate.Source).Equals(
                        workItem.AivStamp.FileStamp))
                {
                    return Failure(
                        LobbyEvaluationFailureKind.AivChangedDuringRead,
                        $"AIV file changed while it was being read: {workItem.Candidate.Source}",
                        mapLookup,
                        aivTimer.Elapsed,
                        TimeSpan.Zero,
                        TimeSpan.Zero);
                }
            }
            else
            {
                if (workItem.AssetText == null)
                {
                    return Failure(
                        LobbyEvaluationFailureKind.AivSourceUnavailable,
                        $"Script Extender AIV asset is unavailable: {workItem.Candidate.Source}",
                        mapLookup,
                        aivTimer.Elapsed,
                        TimeSpan.Zero,
                        TimeSpan.Zero);
                }
                loaded = AivJsonFileLoader.LoadText(
                    workItem.AssetText,
                    workItem.Candidate.Source);
            }

            AivParseResult parsed = new AivBlueprintParser().Parse(
                loaded.Document,
                workItem.Candidate.Source,
                loaded.Diagnostics);
            aivTimer.Stop();
            AivDiagnostic firstError = parsed.Diagnostics.FirstOrDefault(
                value => value.Severity == AivDiagnosticSeverity.Error);
            if (firstError != null)
            {
                return Failure(
                    LobbyEvaluationFailureKind.AivParseFailed,
                    $"{firstError.Code}: {firstError.Message}",
                    mapLookup,
                    aivTimer.Elapsed,
                    TimeSpan.Zero,
                    TimeSpan.Zero);
            }

            try
            {
                TimeSpan projectionElapsed = TimeSpan.Zero;
                TimeSpan ruleElapsed = TimeSpan.Zero;
                var variants = new List<AivPlacementResult>(4);
                AivRotation rotation = workItem.Request.InitialRotation;
                for (int index = 0; index < 4; index++)
                {
                    var projectionTimer = Stopwatch.StartNew();
                    AivProjectedCastle castle = projector.Project(
                        parsed.Blueprint,
                        keep.Coordinate.Value,
                        rotation);
                    projectionTimer.Stop();
                    projectionElapsed += projectionTimer.Elapsed;

                    var ruleTimer = Stopwatch.StartNew();
                    variants.Add(evaluator.Evaluate(mapLookup.Map.Snapshot, castle));
                    ruleTimer.Stop();
                    ruleElapsed += ruleTimer.Elapsed;
                    rotation = NextRotation(rotation);
                }

                AivPlacementRotationSelection selection = evaluator.SelectRotationResults(
                    variants,
                    workItem.Request.InitialRotation);
                return new LobbyPlacementWorkerResult(
                    selection,
                    LobbyEvaluationFailureKind.None,
                    string.Empty,
                    new LobbyPlacementPhaseTimings(
                        mapLookup.MapParse,
                        mapLookup.Snapshot,
                        aivTimer.Elapsed,
                        projectionElapsed,
                        ruleElapsed,
                        mapLookup.CacheHit,
                        mapLookup.Shared));
            }
            catch (Exception ex)
            {
                return Failure(
                    LobbyEvaluationFailureKind.PlacementEvaluationFailed,
                    ex.Message,
                    mapLookup,
                    aivTimer.Elapsed,
                    TimeSpan.Zero,
                    TimeSpan.Zero);
            }
        }

        private PreparedMapLookup GetPreparedMap(LobbyFileStamp stamp)
        {
            Lazy<PreparedMap> lazy;
            bool owner = false;
            lock (mapSync)
            {
                if (mapCache.TryGetValue(stamp, out PreparedMap cached))
                    return new PreparedMapLookup(cached, true, false);
                if (!mapInFlight.TryGetValue(stamp, out lazy))
                {
                    lazy = new Lazy<PreparedMap>(
                        () => PrepareMap(stamp),
                        LazyThreadSafetyMode.ExecutionAndPublication);
                    mapInFlight.Add(stamp, lazy);
                    owner = true;
                }
            }

            PreparedMap map = lazy.Value;
            lock (mapSync)
            {
                if (owner)
                {
                    mapInFlight.Remove(stamp);
                    mapCache.Set(stamp, map);
                }
            }
            return new PreparedMapLookup(map, false, !owner);
        }

        private static PreparedMap PrepareMap(LobbyFileStamp stamp)
        {
            if (!stamp.Exists)
            {
                return PreparedMap.Failure(
                    LobbyEvaluationFailureKind.MapReadFailed,
                    $"Map file is unavailable: {stamp.Path}");
            }

            try
            {
                var parseTimer = Stopwatch.StartNew();
                MapDocument document = MapFileReader.Parse(stamp.Path);
                parseTimer.Stop();
                if (!LobbyFileStamp.Capture(stamp.Path).Equals(stamp))
                {
                    return PreparedMap.Failure(
                        LobbyEvaluationFailureKind.MapChangedDuringRead,
                        $"Map file changed while it was being read: {stamp.Path}",
                        parseTimer.Elapsed,
                        TimeSpan.Zero);
                }

                var snapshotTimer = Stopwatch.StartNew();
                MapPlacementSnapshot snapshot = MapPlacementSnapshot.Create(document);
                MapKeepAnchors anchors = MapKeepAnchors.Create(document);
                snapshotTimer.Stop();
                return PreparedMap.Success(
                    snapshot,
                    anchors,
                    parseTimer.Elapsed,
                    snapshotTimer.Elapsed);
            }
            catch (Exception ex)
            {
                return PreparedMap.Failure(
                    LobbyEvaluationFailureKind.MapSnapshotUnavailable,
                    ex.Message);
            }
        }

        private static LobbyPlacementWorkerResult Failure(
            LobbyEvaluationFailureKind kind,
            string message,
            PreparedMapLookup map,
            TimeSpan aivParse,
            TimeSpan projection,
            TimeSpan rules) =>
            new LobbyPlacementWorkerResult(
                null,
                kind,
                message,
                new LobbyPlacementPhaseTimings(
                    map.MapParse,
                    map.Snapshot,
                    aivParse,
                    projection,
                    rules,
                    map.CacheHit,
                    map.Shared));

        private static AivRotation NextRotation(AivRotation rotation) =>
            rotation == AivRotation.Degrees270
                ? AivRotation.Degrees0
                : (AivRotation)((int)rotation + 90);

        private sealed class PreparedMap
        {
            private PreparedMap(
                MapPlacementSnapshot snapshot,
                MapKeepAnchors anchors,
                LobbyEvaluationFailureKind failureKind,
                string failureMessage,
                TimeSpan mapParse,
                TimeSpan snapshotElapsed)
            {
                Snapshot = snapshot;
                Anchors = anchors;
                FailureKind = failureKind;
                FailureMessage = failureMessage ?? string.Empty;
                MapParse = mapParse;
                SnapshotElapsed = snapshotElapsed;
            }

            public MapPlacementSnapshot Snapshot { get; }
            public MapKeepAnchors Anchors { get; }
            public LobbyEvaluationFailureKind FailureKind { get; }
            public string FailureMessage { get; }
            public TimeSpan MapParse { get; }
            public TimeSpan SnapshotElapsed { get; }
            public bool IsReady => FailureKind == LobbyEvaluationFailureKind.None;

            public static PreparedMap Success(
                MapPlacementSnapshot snapshot,
                MapKeepAnchors anchors,
                TimeSpan mapParse,
                TimeSpan snapshotElapsed) =>
                new PreparedMap(
                    snapshot,
                    anchors,
                    LobbyEvaluationFailureKind.None,
                    string.Empty,
                    mapParse,
                    snapshotElapsed);

            public static PreparedMap Failure(
                LobbyEvaluationFailureKind kind,
                string message,
                TimeSpan mapParse = default,
                TimeSpan snapshotElapsed = default) =>
                new PreparedMap(null, null, kind, message, mapParse, snapshotElapsed);
        }

        private readonly struct PreparedMapLookup
        {
            public PreparedMapLookup(PreparedMap map, bool cacheHit, bool shared)
            {
                Map = map;
                CacheHit = cacheHit;
                Shared = shared;
            }

            public PreparedMap Map { get; }
            public bool CacheHit { get; }
            public bool Shared { get; }
            public TimeSpan MapParse => CacheHit || Shared ? TimeSpan.Zero : Map.MapParse;
            public TimeSpan Snapshot => CacheHit || Shared ? TimeSpan.Zero : Map.SnapshotElapsed;
        }
    }

    internal readonly struct AivPlacementEvaluationKey : IEquatable<AivPlacementEvaluationKey>
    {
        public AivPlacementEvaluationKey(
            LobbyFileStamp map,
            LobbyAivSourceStamp aiv,
            int keepSlot,
            AivRotation rotation,
            int preBuildSetting,
            string analyzerVersion)
        {
            Map = map;
            Aiv = aiv;
            KeepSlot = keepSlot;
            Rotation = rotation;
            PreBuildSetting = preBuildSetting;
            AnalyzerVersion = analyzerVersion;
        }

        public LobbyFileStamp Map { get; }
        public LobbyAivSourceStamp Aiv { get; }
        public int KeepSlot { get; }
        public AivRotation Rotation { get; }
        public int PreBuildSetting { get; }
        public string AnalyzerVersion { get; }

        public bool Equals(AivPlacementEvaluationKey other) =>
            Map.Equals(other.Map) &&
            Aiv.Equals(other.Aiv) &&
            KeepSlot == other.KeepSlot &&
            Rotation == other.Rotation &&
            PreBuildSetting == other.PreBuildSetting &&
            string.Equals(AnalyzerVersion, other.AnalyzerVersion, StringComparison.Ordinal);

        public override bool Equals(object obj) =>
            obj is AivPlacementEvaluationKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Map.GetHashCode();
                hash = hash * 397 ^ Aiv.GetHashCode();
                hash = hash * 397 ^ KeepSlot;
                hash = hash * 397 ^ (int)Rotation;
                hash = hash * 397 ^ PreBuildSetting;
                return hash * 397 ^ (AnalyzerVersion?.GetHashCode() ?? 0);
            }
        }
    }

    internal sealed class BoundedLruCache<TKey, TValue>
    {
        private readonly int capacity;
        private readonly Dictionary<TKey, LinkedListNode<Entry>> entries;
        private readonly LinkedList<Entry> usage = new LinkedList<Entry>();

        public BoundedLruCache(int capacity)
        {
            this.capacity = capacity;
            entries = new Dictionary<TKey, LinkedListNode<Entry>>();
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            if (!entries.TryGetValue(key, out LinkedListNode<Entry> node))
            {
                value = default;
                return false;
            }

            usage.Remove(node);
            usage.AddFirst(node);
            value = node.Value.Value;
            return true;
        }

        public void Set(TKey key, TValue value)
        {
            if (entries.TryGetValue(key, out LinkedListNode<Entry> existing))
            {
                existing.Value = new Entry(key, value);
                usage.Remove(existing);
                usage.AddFirst(existing);
                return;
            }

            var node = new LinkedListNode<Entry>(new Entry(key, value));
            usage.AddFirst(node);
            entries.Add(key, node);
            if (entries.Count <= capacity)
                return;

            LinkedListNode<Entry> oldest = usage.Last;
            usage.RemoveLast();
            entries.Remove(oldest.Value.Key);
        }

        private readonly struct Entry
        {
            public Entry(TKey key, TValue value)
            {
                Key = key;
                Value = value;
            }

            public TKey Key { get; }
            public TValue Value { get; }
        }
    }
}
