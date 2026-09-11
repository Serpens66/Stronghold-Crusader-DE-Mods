// Feature: Add direct JSON sidecars to Vanilla Custom and Extended Lord Workshop uploads.
using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace BugfixesAndQoL
{
    internal sealed class CustomLordJsonUploadHook
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

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly Hook hook;
        private readonly UploadWorkshopMapDelegate trampoline;

        internal CustomLordJsonUploadHook(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

            // COMPATIBILITY: Keep this exact Vanilla signature synchronized with Assembly-CSharp.
            MethodInfo method = typeof(Platform_Workshop).GetMethod(
                nameof(Platform_Workshop.UploadWorkshopMap),
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[]
                {
                    typeof(string), typeof(string), typeof(string), typeof(string[]),
                    typeof(bool), typeof(string), typeof(Action), typeof(Action)
                },
                null) ?? throw new MissingMethodException(
                    typeof(Platform_Workshop).FullName,
                    nameof(Platform_Workshop.UploadWorkshopMap));

            Hook installed = null;
            try
            {
                installed = new Hook(method, (UploadWorkshopMapDelegate)UploadWorkshopMapHook);
                trampoline = installed.GenerateTrampoline<UploadWorkshopMapDelegate>();
                hook = installed;
            }
            catch
            {
                // Only an unpublished initialization candidate may be disposed.
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
            if (!settings.EnableMod)
            {
                CallOriginal(instance, nameMap, mapTitle, description, tags, publicMap, previewImage, successAction, failAction);
                return;
            }

            CustomLordJsonUploadMode mode = CustomLordJsonUploadPolicy.Classify(tags);
            if (mode == CustomLordJsonUploadMode.None)
            {
                CallOriginal(instance, nameMap, mapTitle, description, tags, publicMap, previewImage, successAction, failAction);
                return;
            }

            try
            {
                if (!TryResolveUpload(mode, mapTitle, tags, out string source, out string stagingChild, out string error))
                {
                    FailUpload(mapTitle, error, failAction);
                    return;
                }

                if (!CustomLordJsonUploadPolicy.TryStageDirectJsonFiles(
                        source,
                        nameMap,
                        stagingChild,
                        out int copiedFiles,
                        out int existingFiles,
                        out error))
                {
                    FailUpload(mapTitle, error, failAction);
                    return;
                }

                Shared.DebugLogHelper.LogInfo(
                    log,
                    "Lord Workshop JSON staging ready for [" + mapTitle + "]: " +
                    copiedFiles + " copied, " + existingFiles + " already present.");
                CallOriginal(instance, nameMap, mapTitle, description, tags, publicMap, previewImage, successAction, failAction);
            }
            catch (Exception exception)
            {
                FailUpload(mapTitle, exception.ToString(), failAction);
            }
        }

        private static bool TryResolveUpload(
            CustomLordJsonUploadMode mode,
            string mapTitle,
            string[] tags,
            out string source,
            out string stagingChild,
            out string error)
        {
            source = string.Empty;
            stagingChild = string.Empty;
            if (mode == CustomLordJsonUploadMode.CustomLord)
            {
                int matches = 0;
                foreach (CustomisationFileManager.CustomLord lord in
                         CustomisationFileManager.Instance.GetCustomLords(includeWorkshop: false))
                {
                    if (lord != null && !lord.workshop &&
                        string.Equals(lord.lordName, mapTitle, StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(lord.customPath))
                    {
                        source = lord.customPath;
                        matches++;
                    }
                }

                if (matches == 1)
                {
                    stagingChild = mapTitle;
                    error = string.Empty;
                    return true;
                }

                error = "the local Custom Lord source was not uniquely resolved";
                return false;
            }

            if (!TryResolveExtendedLordType(tags, out int lordType, out stagingChild))
            {
                error = "the Extended Lord path tag was not uniquely resolved";
                return false;
            }

            List<CustomisationFileManager.CustomLordConfig> configs =
                CustomisationFileManager.Instance.getLordLordList(lordType);
            int configMatches = 0;
            if (configs != null)
            {
                foreach (CustomisationFileManager.CustomLordConfig config in configs)
                {
                    if (config != null && !config.workshop &&
                        string.Equals(config.name, mapTitle, StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(config.path))
                    {
                        source = config.path;
                        configMatches++;
                    }
                }
            }

            if (configMatches == 1)
            {
                error = string.Empty;
                return true;
            }

            error = "the local Extended Lord configuration source was not uniquely resolved";
            return false;
        }

        private static bool TryResolveExtendedLordType(
            string[] tags,
            out int lordType,
            out string stagingChild)
        {
            lordType = -1;
            stagingChild = string.Empty;
            int matches = 0;
            if (tags == null)
                return false;

            for (int index = 0; index < ConfigSettings.extendedLordPaths.Length; index++)
            {
                foreach (string tag in tags)
                {
                    if (string.Equals(tag, ConfigSettings.extendedLordPaths[index], StringComparison.Ordinal))
                    {
                        lordType = index;
                        stagingChild = ConfigSettings.extendedLordPaths[index];
                        matches++;
                    }
                }
            }
            return matches == 1;
        }

        private void FailUpload(string mapTitle, string error, Action failAction)
        {
            Shared.DebugLogHelper.LogError(
                log,
                "Lord Workshop upload [" + mapTitle + "] cancelled because JSON staging failed: " + error);
            try
            {
                failAction?.Invoke();
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    "Vanilla's Lord Workshop failure callback failed for [" + mapTitle + "]: " + exception);
            }
        }

        private void CallOriginal(
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
            trampoline(instance, nameMap, mapTitle, description, tags, publicMap, previewImage, successAction, failAction);
        }
    }
}
