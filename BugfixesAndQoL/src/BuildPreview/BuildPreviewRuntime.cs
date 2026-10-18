using BepInEx;
using BepInEx.Logging;
using SHCDESE.API.LowLevel;
using SHCDESE.Interop;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;

namespace BugfixesAndQoL.BuildPreview
{
    // Owned for the process lifetime. LibraryLoaded installs once; the setting publisher
    // only changes immutable logical state, never patches or tears down executable code.
    internal static class BuildPreviewRuntime
    {
        private const string Name = "Improved building preview";
        internal static readonly BuildPreviewStateStore State = new BuildPreviewStateStore();
        private static BugfixesAndQoLViewModel settings;
        private static int ready;
        private static ManualLogSource persistentLog;
        private static NativeDetour<PreviewNativeContract.PreviewDelegate> permanentHook;
        private static readonly PreviewNativeContract.PreviewDelegate callback = Preview;
        private static ulong imageBase;
        private static int attempted, confirmed, errorReported;
        internal static void Configure(ManualLogSource log, BugfixesAndQoLViewModel model)
        {
            persistentLog = log; settings = model;
            GroundTooltipRuntime.Initialize(log);
            settings.SettingChanged += OnSettingChanged;
        }
        private static void OnSettingChanged(string property)
        {
            if (property == nameof(settings.EnableClientFeatures) || property == nameof(settings.EnableImprovedBuildPreview))
                RefreshActivation();
        }
        private static void RefreshActivation() => State.SetEnabled(Volatile.Read(ref ready) != 0 &&
            BuildPreviewStateStore.Effective(settings.EnableClientFeatures, settings.EnableImprovedBuildPreview));
        internal static void Install(CrusaderLibraryLoadContext context)
        {
            if (Interlocked.Exchange(ref attempted, 1) != 0) return;
            GroundTooltipRuntime.InstallPresentation();
            NativeDetour<PreviewNativeContract.PreviewDelegate> candidate = null;
            bool published = false;
            try
            {
                string path = Path.Combine(Paths.GameRootPath,
                    "Stronghold Crusader Definitive Edition_Data", "Plugins", "x86_64", "CrusaderDE.dll");
                using (var stream = File.OpenRead(path))
                using (var sha = SHA256.Create())
                    if (BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "") != PreviewNativeContract.NativeHash)
                        throw new InvalidOperationException("Unreviewed native hash; building preview remains Vanilla.");
                var resolution = Shared.NativePatternResolver.ResolveUnique(context.Memory,
                    PreviewNativeContract.Signature, PreviewNativeContract.PreviewRva, true, Name);
                if (resolution.Rva != PreviewNativeContract.PreviewRva)
                    throw new InvalidOperationException("Preview resolved outside the hash-bound audited function.");
                imageBase = (ulong)context.ModuleHandle.ToInt64();
                ulong target = imageBase + PreviewNativeContract.PreviewRva;
                PreviewNativeContract.ValidateEntry(target);
                var request = new DetourRequest<PreviewNativeContract.PreviewDelegate>
                {
                    Name = Name, TargetAddress = target, Callback = callback
                };
                candidate = (NativeDetour<PreviewNativeContract.PreviewDelegate>)PreviewNativeContract.Backend().CreateDetour(in request);
                PreviewNativeContract.Validate(candidate, target, false);
                // The inactive wrapper can safely call Original even during installation.
                permanentHook = candidate;
                candidate.Enable();
                PreviewNativeContract.Validate(candidate, target, true);
                published = true;
                Volatile.Write(ref ready, 1);
                GroundTooltipRuntime.SetNativeReady(true);
                RefreshActivation();
            }
            catch (Exception error)
            {
                Volatile.Write(ref ready, 0);
                State.SetEnabled(false);
                GroundTooltipRuntime.SetNativeReady(false);
                if (!published)
                {
                    candidate?.Dispose(); // Failed, unpublished initialization candidate only.
                    permanentHook = null;
                }
                Shared.DebugLogHelper.LogError(persistentLog, "BUILD_PREVIEW_DISABLED: " + error);
            }
        }

        private static unsafe void Preview(IntPtr manager, int x, int y, short mapper, int scale)
        {
            var capture = State.Read();
            bool eligible = false;
            bool blocked = false;
            try
            {
                eligible = capture.Enabled && capture.Session != 0 && FarmPreviewPolicy.Scale((eMappers)mapper) == scale &&
                    scale != 0 && (ulong)manager.ToInt64() == imageBase + PreviewNativeContract.ManagerRva &&
                    Marshal.ReadInt32(manager, PreviewNativeContract.PreviewOverrideOffset) == 0 &&
                    Marshal.ReadInt32(manager, PreviewNativeContract.PlacementStateOffset) != 2;
                if (eligible) blocked = Marshal.ReadInt32(manager, PreviewNativeContract.PlacementStateOffset) != 0;
            }
            catch (Exception error)
            {
                Volatile.Write(ref ready, 0);
                State.SetEnabled(false);
                GroundTooltipRuntime.SetNativeReady(false);
                ReportError(error);
            }

            permanentHook.Original(manager, x, y, mapper, scale);

            // Augmentation and diagnostics can never suppress or repeat Vanilla.
            try
            {
                if (eligible && Volatile.Read(ref confirmed) == 0 && Interlocked.CompareExchange(ref confirmed, 1, 0) == 0)
                    Shared.DebugLogHelper.LogDebug(persistentLog, "BUILD_PREVIEW_RUNTIME_CONFIRMED native preview after startup cleanup.");
                if (eligible && State.IsCurrent(capture))
                {
                    MarkResult result = FootprintMarker.Apply(imageBase, manager, x, y, scale, (eMappers)mapper, blocked);
                    State.Publish(capture, (eMappers)mapper, x, y, result.Counts);
                }
                else State.Clear(capture);
            }
            catch (Exception error)
            {
                Volatile.Write(ref ready, 0);
                State.SetEnabled(false); // The published hook still executes the complete Vanilla path.
                GroundTooltipRuntime.SetNativeReady(false);
                ReportError(error);
            }
        }

        private static void ReportError(Exception error)
        {
            if (Interlocked.Exchange(ref errorReported, 1) == 0)
                Shared.DebugLogHelper.LogError(persistentLog, "BUILD_PREVIEW_CALLBACK_ERROR: " + error);
        }
    }
}
