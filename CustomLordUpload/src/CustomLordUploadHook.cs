using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace CustomLordUpload
{
    internal sealed class CustomLordUploadHook
    {
        private delegate void UploadWorkshopMapDelegate(
            Platform_Workshop instance,
            string nameMap,
            string mapTitle,
            string description,
            string[] tags,
            bool publicMap,
            string previewImage,
            Action successAction,
            Action failAction);

        private static readonly object ApprovalLock = new object();
        private static readonly HashSet<string> ApprovedUploads =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly ManualLogSource log;
        private readonly Hook hook;
        private readonly UploadWorkshopMapDelegate trampoline;

        internal CustomLordUploadHook(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            MethodInfo? method = typeof(Platform_Workshop).GetMethod(
                nameof(Platform_Workshop.UploadWorkshopMap),
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[]
                {
                    typeof(string), typeof(string), typeof(string), typeof(string[]),
                    typeof(bool), typeof(string), typeof(Action), typeof(Action)
                },
                null);
            if (method == null)
                throw new MissingMethodException(typeof(Platform_Workshop).FullName, nameof(Platform_Workshop.UploadWorkshopMap));

            Hook? installed = null;
            try
            {
                installed = new Hook(method, (UploadWorkshopMapDelegate)UploadWorkshopMapHook);
                trampoline = installed.GenerateTrampoline<UploadWorkshopMapDelegate>();
                hook = installed;
            }
            catch
            {
                installed?.Dispose();
                throw;
            }
        }

        private void UploadWorkshopMapHook(
            Platform_Workshop instance,
            string nameMap,
            string mapTitle,
            string description,
            string[] tags,
            bool publicMap,
            string previewImage,
            Action successAction,
            Action failAction)
        {
            if (CustomLordWorkshopPackagePolicy.IsCustomLordUpload(tags))
            {
                try
                {
                    string uploadKey = GetUploadKey(nameMap, mapTitle);
                    if (IsApproved(uploadKey))
                    {
                        ContinueUpload(
                            uploadKey, true, instance, nameMap, mapTitle, description,
                            tags, publicMap, previewImage, successAction, failAction);
                        return;
                    }

                    IReadOnlyList<string> issues = InspectUpload(mapTitle);
                    if (issues.Count > 0)
                    {
                        ShowConfirmation(
                            uploadKey, issues, instance, nameMap, mapTitle, description,
                            tags, publicMap, previewImage, successAction, failAction);
                        return;
                    }

                    TryExtendStaging(nameMap, mapTitle);
                }
                catch (Exception exception)
                {
                    Warning($"Extended Custom Lord files were omitted; Vanilla upload continues: {exception.Message}");
                }
            }

            trampoline(instance, nameMap, mapTitle, description, tags, publicMap, previewImage, successAction, failAction);
        }

        private void ShowConfirmation(
            string uploadKey,
            IReadOnlyList<string> issues,
            Platform_Workshop instance,
            string nameMap,
            string mapTitle,
            string description,
            string[] tags,
            bool publicMap,
            string previewImage,
            Action successAction,
            Action failAction)
        {
            foreach (string issue in issues)
                Warning($"Custom Lord upload preflight [{mapTitle}]: {issue}");

            bool german = GameAssetManagerAPI.Instance.CurrentLanguage.StartsWith(
                "de", StringComparison.OrdinalIgnoreCase);
            string title = german ? "Custom-Lord-Upload: Warnungen" : "Custom Lord upload warnings";
            StringBuilder message = new StringBuilder();
            message.AppendLine(german
                ? "Die folgenden möglichen Paketprobleme wurden gefunden:"
                : "The following possible package problems were found:");
            message.AppendLine();
            for (int index = 0; index < issues.Count; index++)
                message.Append(index + 1).Append(". ").AppendLine(issues[index]);
            message.AppendLine();
            message.Append(german ? "Dennoch uploaden?" : "Upload anyway?");

            try
            {
                HUD_ConfirmationPopup.ShowConfirmationMessage(
                    title,
                    () => ContinueUpload(
                        uploadKey, false, instance, nameMap, mapTitle, description,
                        tags, publicMap, previewImage, successAction, failAction),
                    () => CancelUpload(mapTitle, failAction),
                    message.ToString(),
                    MPConf: false,
                    tall: true);
            }
            catch (Exception exception)
            {
                Error($"Could not display Custom Lord upload warnings for [{mapTitle}]: {exception}");
                CancelUpload(mapTitle, failAction);
            }
        }

        private void ContinueUpload(
            string uploadKey,
            bool approvalAlreadyActive,
            Platform_Workshop instance,
            string nameMap,
            string mapTitle,
            string description,
            string[] tags,
            bool publicMap,
            string previewImage,
            Action successAction,
            Action failAction)
        {
            Action effectiveSuccess = successAction;
            Action effectiveFailure = failAction;
            bool ownsApproval = false;
            if (!approvalAlreadyActive)
            {
                lock (ApprovalLock)
                    ownsApproval = ApprovedUploads.Add(uploadKey);

                if (ownsApproval)
                {
                    effectiveSuccess = () =>
                    {
                        EndApproval(uploadKey);
                        successAction?.Invoke();
                    };
                    effectiveFailure = () =>
                    {
                        EndApproval(uploadKey);
                        failAction?.Invoke();
                    };
                    Info($"Custom Lord upload warnings accepted for [{mapTitle}].");
                }
            }

            try
            {
                TryExtendStaging(nameMap, mapTitle);
                trampoline(
                    instance, nameMap, mapTitle, description, tags, publicMap,
                    previewImage, effectiveSuccess, effectiveFailure);
            }
            catch (Exception exception)
            {
                Error($"Could not start the approved Custom Lord upload for [{mapTitle}]: {exception}");
                try
                {
                    effectiveFailure?.Invoke();
                }
                finally
                {
                    if (ownsApproval)
                        EndApproval(uploadKey);
                }
            }
        }

        private IReadOnlyList<string> InspectUpload(string mapTitle)
        {
            try
            {
                if (!TryResolveSource(mapTitle, out string sourcePath, out string error))
                    return new[] { error + " Only Vanilla's staged base files can be uploaded." };
                return CustomLordWorkshopPreflight.Inspect(sourcePath);
            }
            catch (Exception exception)
            {
                return new[]
                {
                    "The package preflight failed unexpectedly and cannot confirm that the extended files are usable: " +
                    exception.Message
                };
            }
        }

        private void CancelUpload(string mapTitle, Action failAction)
        {
            Warning($"Custom Lord upload [{mapTitle}] was cancelled after preflight warnings.");
            failAction?.Invoke();
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

        private void TryExtendStaging(string uploadContentRoot, string mapTitle)
        {
            if (!CustomLordWorkshopPackagePolicy.IsSafeDirectoryName(mapTitle))
            {
                Warning("Extended Custom Lord files were omitted because the upload title is not a safe folder name.");
                return;
            }

            if (string.IsNullOrWhiteSpace(uploadContentRoot) || !Path.IsPathRooted(uploadContentRoot))
            {
                Warning("Extended Custom Lord files were omitted because Vanilla's staging path is invalid.");
                return;
            }

            if (!TryResolveSource(mapTitle, out string sourcePath, out string sourceError))
            {
                Warning("Extended Custom Lord files were omitted because " + sourceError);
                return;
            }

            string stagingRoot = Path.GetFullPath(uploadContentRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string destination = Path.GetFullPath(Path.Combine(stagingRoot, mapTitle));
            string stagingPrefix = stagingRoot + Path.DirectorySeparatorChar;
            if (!Directory.Exists(stagingRoot) ||
                !destination.StartsWith(stagingPrefix, StringComparison.OrdinalIgnoreCase) ||
                !Directory.Exists(destination) ||
                (File.GetAttributes(stagingRoot) & FileAttributes.ReparsePoint) != 0 ||
                (File.GetAttributes(destination) & FileAttributes.ReparsePoint) != 0)
            {
                Warning("Extended Custom Lord files were omitted because Vanilla's staging directory is unsafe or incomplete.");
                return;
            }

            if (!CustomLordWorkshopPackagePolicy.TryStageFiles(
                    sourcePath, destination,
                    out int copiedFileCount, out int existingFileCount, out string error))
            {
                Warning($"Extended Custom Lord files were omitted from [{mapTitle}]; Vanilla upload continues: {error}");
                return;
            }

            Info(
                $"Extended Custom Lord Workshop staging is ready for [{mapTitle}] " +
                $"({copiedFileCount} copied, {existingFileCount} already present; Vanilla base files unchanged).");
        }

        private static bool TryResolveSource(string mapTitle, out string sourcePath, out string error)
        {
            sourcePath = string.Empty;
            List<CustomisationFileManager.CustomLord> localLords =
                CustomisationFileManager.Instance.GetCustomLords(includeWorkshop: false);
            int matchCount = 0;
            foreach (CustomisationFileManager.CustomLord lord in localLords)
            {
                if (lord != null && !lord.workshop &&
                    string.Equals(lord.lordName, mapTitle, StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(lord.customPath))
                        sourcePath = lord.customPath;
                    matchCount++;
                }
            }

            if (matchCount != 1 || string.IsNullOrWhiteSpace(sourcePath))
            {
                error = $"source [{mapTitle}] was not uniquely resolved among local non-Workshop lords.";
                sourcePath = string.Empty;
                return false;
            }

            error = string.Empty;
            return true;
        }

        private void Info(string message) => Shared.DebugLogHelper.LogInfo(log, message);
        private void Warning(string message) => Shared.DebugLogHelper.LogWarning(log, message);
        private void Error(string message) => Shared.DebugLogHelper.LogError(log, message);
    }
}
