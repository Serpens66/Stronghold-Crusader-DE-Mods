using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace CustomLordUpload
{
    internal sealed class CustomLordUploadWorkflow
    {
        private static readonly object ApprovalLock = new object();
        // COMPATIBILITY: Approval spans Vanilla's possible recursive/retry upload call and ends in its callbacks.
        private static readonly HashSet<string> ApprovedUploads =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static long nextUploadOperationId;

        private readonly ManualLogSource log;
        private readonly ICustomLordUploadStager stager;
        private readonly ICustomLordUploadConfirmation confirmation;
        private readonly CustomLordRuntimeRules rules;

        internal CustomLordUploadWorkflow(
            ManualLogSource log,
            ICustomLordUploadStager stager,
            ICustomLordUploadConfirmation confirmation,
            CustomLordRuntimeRules rules)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.stager = stager ?? throw new ArgumentNullException(nameof(stager));
            this.confirmation = confirmation ?? throw new ArgumentNullException(nameof(confirmation));
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
        }

        internal void Handle(CustomLordUploadRequest request, Action<CustomLordUploadRequest> callOriginal)
        {
            if (!IsCustomLordUpload(request.Tags))
            {
                callOriginal(request);
                return;
            }

            try
            {
                string uploadKey = GetUploadKey(request.NameMap, request.MapTitle);
                if (IsApproved(uploadKey))
                {
                    ContinueUpload(uploadKey, true, request, callOriginal);
                    return;
                }

                IReadOnlyList<CustomLordUploadIssue> issues = InspectUpload(request.MapTitle);
                if (issues.Count > 0)
                {
                    ShowConfirmation(uploadKey, issues, request, callOriginal);
                    return;
                }

                TryExtendStaging(request.NameMap, request.MapTitle);
            }
            catch (Exception exception)
            {
                Warning("Extended Custom Lord files were omitted; Vanilla upload continues: " + exception);
            }

            StartVanillaUpload(request, callOriginal, releaseApprovalKey: null);
        }

        internal static bool IsCustomLordUpload(string[]? tags)
        {
            // COMPATIBILITY: Steam/Vanilla currently route Custom Lords with this exact tag.
            if (tags == null)
                return false;
            foreach (string tag in tags)
            {
                if (string.Equals(tag, "Custom Lord", StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private IReadOnlyList<CustomLordUploadIssue> InspectUpload(string mapTitle)
        {
            try
            {
                if (!stager.TryResolveSource(mapTitle, out string sourcePath, out string error))
                    return new[] { new CustomLordUploadIssue("SourceNotUnique", error, "Lord", mapTitle) };
                return CustomLordWorkshopPreflight.Inspect(sourcePath, rules);
            }
            catch (Exception exception)
            {
                return new[] { new CustomLordUploadIssue("UnexpectedPreflight", exception.ToString()) };
            }
        }

        private void ShowConfirmation(
            string uploadKey,
            IReadOnlyList<CustomLordUploadIssue> issues,
            CustomLordUploadRequest request,
            Action<CustomLordUploadRequest> callOriginal)
        {
            foreach (CustomLordUploadIssue issue in issues)
            {
                string detail = string.IsNullOrWhiteSpace(issue.TechnicalDetail)
                    ? string.Empty
                    : " Detail: " + issue.TechnicalDetail;
                Warning(
                    "Custom Lord upload preflight [" + request.MapTitle + "] [" + issue.Code + "]: " +
                    issue.Format() + detail);
            }

            try
            {
                confirmation.Show(
                    issues,
                    () => ContinueUpload(uploadKey, false, request, callOriginal),
                    () => CancelUpload(request));
            }
            catch (Exception exception)
            {
                Error("Could not display Custom Lord upload warnings for [" + request.MapTitle + "]: " + exception);
                CancelUpload(request);
            }
        }

        private void ContinueUpload(
            string uploadKey,
            bool approvalAlreadyActive,
            CustomLordUploadRequest request,
            Action<CustomLordUploadRequest> callOriginal)
        {
            bool ownsApproval = false;
            if (!approvalAlreadyActive)
            {
                lock (ApprovalLock)
                    ownsApproval = ApprovedUploads.Add(uploadKey);

                if (ownsApproval)
                {
                    Info("Custom Lord upload warnings accepted for [" + request.MapTitle + "].");
                }
            }

            try
            {
                TryExtendStaging(request.NameMap, request.MapTitle);
                StartVanillaUpload(
                    request,
                    callOriginal,
                    ownsApproval ? uploadKey : null);
            }
            catch (Exception exception)
            {
                Error("Could not start the approved Custom Lord upload for [" + request.MapTitle + "]: " + exception);
                try
                {
                    request.FailAction?.Invoke();
                }
                finally
                {
                    if (ownsApproval)
                        EndApproval(uploadKey);
                }
            }
        }

        // COMPATIBILITY: Recheck that Vanilla reports every terminal Workshop result through exactly
        // one of these callbacks after game-DLL updates; the callback signatures come from UploadWorkshopMap.
        private void StartVanillaUpload(
            CustomLordUploadRequest request,
            Action<CustomLordUploadRequest> callOriginal,
            string? releaseApprovalKey)
        {
            long operationId = Interlocked.Increment(ref nextUploadOperationId);
            long startedAt = Stopwatch.GetTimestamp();
            int callbackState = 0;

            Action completeSuccess = () =>
            {
                if (Interlocked.CompareExchange(ref callbackState, 1, 0) != 0)
                {
                    Warning("Custom Lord upload #" + operationId + " [" + request.MapTitle +
                        "] received a duplicate success callback; it was ignored.");
                    return;
                }

                if (releaseApprovalKey != null)
                    EndApproval(releaseApprovalKey);
                Info("Custom Lord upload #" + operationId + " [" + request.MapTitle +
                    "] completed successfully after " + FormatElapsedMilliseconds(startedAt) + " ms.");
                try
                {
                    request.SuccessAction?.Invoke();
                }
                catch (Exception exception)
                {
                    Error("Vanilla's Custom Lord upload success callback failed for #" +
                        operationId + " [" + request.MapTitle + "]: " + exception);
                }
            };

            Action completeFailure = () =>
            {
                if (Interlocked.CompareExchange(ref callbackState, 2, 0) != 0)
                {
                    Warning("Custom Lord upload #" + operationId + " [" + request.MapTitle +
                        "] received a duplicate failure callback; it was ignored.");
                    return;
                }

                if (releaseApprovalKey != null)
                    EndApproval(releaseApprovalKey);
                Warning("Custom Lord upload #" + operationId + " [" + request.MapTitle +
                    "] failed after " + FormatElapsedMilliseconds(startedAt) + " ms.");
                try
                {
                    request.FailAction?.Invoke();
                }
                catch (Exception exception)
                {
                    Error("Vanilla's Custom Lord upload failure callback failed for #" +
                        operationId + " [" + request.MapTitle + "]: " + exception);
                }
            };

            CustomLordUploadRequest trackedRequest = request.WithCallbacks(
                completeSuccess,
                completeFailure);
            Info("Custom Lord upload #" + operationId + " [" + request.MapTitle +
                "] handed to Vanilla/Steam; awaiting completion callback.");
            try
            {
                callOriginal(trackedRequest);
            }
            catch (Exception exception)
            {
                Error("Could not hand Custom Lord upload #" + operationId + " [" +
                    request.MapTitle + "] to Vanilla/Steam: " + exception);
                completeFailure();
            }
        }

        private static long FormatElapsedMilliseconds(long startedAt)
        {
            long elapsedTicks = Stopwatch.GetTimestamp() - startedAt;
            return elapsedTicks <= 0
                ? 0
                : (long)(elapsedTicks * 1000d / Stopwatch.Frequency);
        }

        private void TryExtendStaging(string uploadContentRoot, string mapTitle)
        {
            if (!stager.TryExtendStaging(
                    uploadContentRoot,
                    mapTitle,
                    out CustomLordUploadStagingSummary? summary,
                    out string error))
            {
                Warning(
                    "Extended Custom Lord files were omitted from [" + mapTitle +
                    "]; Vanilla upload continues: " + error);
                return;
            }

            if (summary == null)
            {
                Warning("Extended Custom Lord files were omitted from [" + mapTitle +
                    "]; Vanilla upload continues: staging completed without a summary");
                return;
            }

            Info(
                "Extended Custom Lord Workshop staging is ready for [" + mapTitle + "] (" +
                summary.PackageFileCount + " extra files, " + summary.PackageByteCount + " bytes; " +
                summary.CopiedFileCount + " copied, " + summary.ExistingFileCount +
                " already present; Vanilla base files unchanged). Source=[" + summary.SourcePath +
                "], staging=[" + summary.DestinationPath + "].");
        }

        private void CancelUpload(CustomLordUploadRequest request)
        {
            Warning("Custom Lord upload [" + request.MapTitle + "] was cancelled after preflight warnings.");
            request.FailAction?.Invoke();
        }

        private static string GetUploadKey(string uploadContentRoot, string mapTitle)
        {
            string root;
            try { root = Path.GetFullPath(uploadContentRoot ?? string.Empty); }
            catch { root = uploadContentRoot ?? string.Empty; }
            return root + "\n" + (mapTitle ?? string.Empty);
        }

        private static bool IsApproved(string uploadKey)
        {
            lock (ApprovalLock)
                return ApprovedUploads.Contains(uploadKey);
        }

        private static void EndApproval(string uploadKey)
        {
            lock (ApprovalLock)
                ApprovedUploads.Remove(uploadKey);
        }

        private void Info(string message) => Shared.DebugLogHelper.LogInfo(log, message);
        private void Warning(string message) => Shared.DebugLogHelper.LogWarning(log, message);
        private void Error(string message) => Shared.DebugLogHelper.LogError(log, message);
    }
}
