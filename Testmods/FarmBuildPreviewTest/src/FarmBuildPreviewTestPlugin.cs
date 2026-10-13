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

namespace FarmBuildPreviewTest
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency("000shcdese", "2.14.0")]
    [BepInDependency("APIShared_Serp", "0.9.0")]
    [BepInDependency("BuildingCosts_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("BuildingLimit_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class FarmBuildPreviewTestPlugin : BaseUnityPlugin
    {
        public const string Guid = "FarmBuildPreviewTest_Serp";
        public const string Name = "Farm Build Preview Test";
        public const string Version = "0.1.0";
        private static ManualLogSource persistentLog;
        private static NativeDetour<PreviewNativeContract.PreviewDelegate> permanentHook;
        private static readonly PreviewNativeContract.PreviewDelegate callback = Preview;
        private static ulong imageBase;
        private static int active, attempted, confirmed, errorReported, changedReported;
        private static bool configuredEnabled;

        private void Awake()
        {
            persistentLog = Logger;
            configuredEnabled = Config.Bind("General", "Enabled", true,
                "Enable red farm terrain preview. Restart the game after changing this setting.").Value;
            GroundTooltipRuntime.Initialize(persistentLog, configuredEnabled);
            CrusaderLibrary.Instance.LibraryLoaded += Install;
            Shared.DebugLogHelper.LogInfo(persistentLog, Name + " " + Version + " waiting for native library.");
        }

        private static void Install(CrusaderLibraryLoadContext context)
        {
            if (Interlocked.Exchange(ref attempted, 1) != 0) return;
            GroundTooltipRuntime.InstallPresentation(configuredEnabled);
            NativeDetour<PreviewNativeContract.PreviewDelegate> candidate = null;
            bool published = false;
            try
            {
                string path = Path.Combine(Paths.GameRootPath,
                    "Stronghold Crusader Definitive Edition_Data", "Plugins", "x86_64", "CrusaderDE.dll");
                using (var stream = File.OpenRead(path))
                using (var sha = SHA256.Create())
                    if (BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "") != PreviewNativeContract.NativeHash)
                        throw new InvalidOperationException("Unreviewed native hash; farm preview remains Vanilla.");
                var resolution = Shared.NativePatternResolver.ResolveUnique(context.Memory,
                    PreviewNativeContract.Signature, PreviewNativeContract.PreviewRva, true, Name, persistentLog);
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
                GroundTooltipRuntime.SetNativeReady(configuredEnabled);
                Volatile.Write(ref active, configuredEnabled ? 1 : 0);
                Shared.DebugLogHelper.LogInfo(persistentLog,
                    "FARM_PREVIEW_READY rva=0x5DD70 scheme=Indirect displaced=6 continuation=0x5DD76 enabled=" + configuredEnabled);
            }
            catch (Exception error)
            {
                if (!published)
                {
                    Volatile.Write(ref active, 0);
                    candidate?.Dispose(); // Failed, unpublished initialization candidate only.
                    permanentHook = null;
                }
                Shared.DebugLogHelper.LogError(persistentLog, "FARM_PREVIEW_DISABLED: " + error);
            }
        }

        private static void Preview(IntPtr manager, int x, int y, short mapper, int scale)
        {
            long session = GroundTooltipRuntime.Session;
            bool eligible = false;
            bool blocked = false;
            try
            {
                eligible = Volatile.Read(ref active) != 0 && FarmPreviewPolicy.Scale((eMappers)mapper) == scale &&
                    scale != 0 && (ulong)manager.ToInt64() == imageBase + PreviewNativeContract.ManagerRva &&
                    Marshal.ReadInt32(manager, PreviewNativeContract.PreviewOverrideOffset) == 0 &&
                    Marshal.ReadInt32(manager, PreviewNativeContract.PlacementStateOffset) != 2;
                if (eligible) blocked = Marshal.ReadInt32(manager, PreviewNativeContract.PlacementStateOffset) != 0;
            }
            catch (Exception error) { ReportError(error); }

            permanentHook.Original(manager, x, y, mapper, scale);

            // Augmentation and diagnostics can never suppress or repeat Vanilla.
            try
            {
                if (FarmPreviewPolicy.Scale((eMappers)mapper) != 0 && Interlocked.Exchange(ref confirmed, 1) == 0)
                    Shared.DebugLogHelper.LogInfo(persistentLog,
                        $"FARM_PREVIEW_RUNTIME_CONFIRMED hook confirmed after startup cleanup; mapper={(eMappers)mapper} x={x} y={y} scale={scale} thread={Thread.CurrentThread.ManagedThreadId}");
                if (eligible) MarkFootprint(manager, x, y, (eMappers)mapper, scale, blocked, session);
                else GroundTooltipRuntime.Publish(null);
            }
            catch (Exception error)
            {
                GroundTooltipRuntime.Publish(null);
                Volatile.Write(ref active, 0); // Permanent hook remains installed and calls Vanilla.
                ReportError(error);
            }
        }

        private static unsafe void MarkFootprint(IntPtr manager, int x, int y, eMappers mapper, int scale, bool blocked, long session)
        {
            MarkResult result = FootprintMarker.Apply(imageBase, manager, x, y, scale, mapper, blocked);
            GroundTooltipRuntime.Publish(new GroundSnapshot(session, mapper, x, y, result.Counts));
            if (result.Changed != 0 && Interlocked.Exchange(ref changedReported, 1) == 0)
                Shared.DebugLogHelper.LogInfo(persistentLog,
                    $"FARM_PREVIEW_MARKED x={x} y={y} cells={result.Cells} coverage={result.Coverage} eligible={result.Eligible} changed={result.Changed} remaining={result.Eligible - result.Changed}");
        }

        private static void ReportError(Exception error)
        {
            if (Interlocked.Exchange(ref errorReported, 1) == 0)
                Shared.DebugLogHelper.LogError(persistentLog, "FARM_PREVIEW_CALLBACK_ERROR: " + error);
        }
    }
}
