using APIShared;
using APIShared.Presentation;
using BepInEx.Logging;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Extensions;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.Threading;

namespace BugfixesAndQoL.BuildPreview
{
    internal static class GroundTooltipRuntime
    {
        private static IReadOnlyList<BuildingTooltipLine> lines = Array.Empty<BuildingTooltipLine>();
        private static string lastLocale;
        private static eMappers lastMapper;
        private static GroundCounts lastCounts;
        private static bool lastActual;
        private static int unityThread, nativeReady, failed;
        private static ManualLogSource log;
        private static IMissionLifecycleCapability lifecycle;
        internal static void SetNativeReady(bool ready) => Volatile.Write(ref nativeReady, ready ? 1 : 0);
        internal static void Initialize(ManualLogSource logger)
        { log = logger; unityThread = Thread.CurrentThread.ManagedThreadId; }
        internal static void InstallPresentation()
        {
            // Always register, including when initially disabled. The shared publisher
            // survives startup cleanup and reevaluates our logical contribution after toggles.
            try
            {
                if (Thread.CurrentThread.ManagedThreadId != unityThread) throw new InvalidOperationException("Presentation registration requires the startup Unity thread.");
                if (!ApiShared.ForMod(BugfixesAndQoLPlugin.PluginGuid).TryGetMissionLifecycle(out lifecycle, out var diagnostic))
                    throw new InvalidOperationException(diagnostic.ToString());
                if (!lifecycle.TryRegisterObserver("GroundTooltip", StartSession, EndSession, Initializing, out diagnostic))
                    throw new InvalidOperationException(diagnostic.ToString());
                if (!BuildingTooltipPresentation.TryRegister(BugfixesAndQoLPlugin.PluginGuid, "Ground", Presented, out string reason, int.MaxValue))
                    throw new InvalidOperationException(reason);
            }
            catch (Exception error) { Fail(error); }
        }
        private static void StartSession(MissionLifecycleNotification args)
        { BuildPreviewRuntime.State.SetSession(args.Context.SessionId); }
        private static void EndSession(MissionLifecycleNotification args)
        { BuildPreviewRuntime.State.SetSession(0); }
        private static void Initializing(MissionLifecycleNotification args)
        { if (args.IsBeforeInitialization) EndSession(args); }
        private static IReadOnlyList<BuildingTooltipLine> Presented(BuildingTooltipContext context)
        {
            if (Thread.CurrentThread.ManagedThreadId != unityThread) return Array.Empty<BuildingTooltipLine>();
            try
            {
                var capture = BuildPreviewRuntime.State.Read();
                if (!capture.Enabled || Volatile.Read(ref failed) != 0 || Volatile.Read(ref nativeReady) == 0 || capture.Session == 0)
                { return Array.Empty<BuildingTooltipLine>(); }
                bool hovering = context.IsSymbolHover;
                eMappers mapper = ((eStructs)context.BuildingType).ConvertToEMappers();
                int scale = FarmPreviewPolicy.Scale(mapper);
                var controls = MainControls.instance;
                bool placing = controls != null && controls.CurrentAction == 5;
                if (!placing) BuildPreviewRuntime.State.Clear(capture);
                if (scale == 0) { BuildPreviewRuntime.State.Clear(capture); return Array.Empty<BuildingTooltipLine>(); }
                int x = 0, y = 0;
                bool validPosition = !hovering && placing && controls.CurrentSubAction == (int)mapper &&
                    !controls.overGUI && !controls.isOffWorld() && GameMap.instance != null && !GameMap.instance.pendingRotationtriggered;
                if (validPosition)
                {
                    float mx = 0, my = 0;
                    controls.getMouseMapTilePosition(ref mx, ref my);
                    var tile = GameMap.instance.getMapTile((int)mx, (int)my);
                    validPosition = tile != null;
                    if (validPosition) { x = tile.gameMapX - scale/2; y = tile.gameMapY - scale/2; }
                }
                var current = capture.Snapshot;
                if (current != null && (!placing || controls.CurrentSubAction != (int)current.Mapper ||
                    (!hovering && (!validPosition || current.X != x || current.Y != y))))
                {
                    BuildPreviewRuntime.State.Clear(capture);
                    current = null;
                }
                bool actual = GroundTooltipModel.Matches(current, capture.Session, mapper, x, y, hovering, validPosition, capture.Generation);
                bool farm = FarmPreviewPolicy.IsFarm(mapper);
                int minimum = FarmPreviewPolicy.Required(mapper);
                int? first = actual ? (int?)(farm ? current.Counts.Coverage : current.Counts.Resource) : null;
                string locale = SerpLocalization.GetActiveLocale();
                GroundCounts counts = actual ? current.Counts : default;
                if (lines.Count != 0 && mapper == lastMapper && actual == lastActual && locale == lastLocale &&
                    counts.Coverage == lastCounts.Coverage && counts.Green == lastCounts.Green && counts.Resource == lastCounts.Resource) return lines;
                string firstText = GroundTooltipModel.Value(SerpLocalization.Get(farm ? "Ground.Coverage" : GroundTooltipModel.LabelKey(mapper)), first, minimum, locale == "ar");
                string secondText = farm ? " \u00b7 " + GroundTooltipModel.Value(SerpLocalization.Get("Ground.Green"), actual ? (int?)current.Counts.Green : null, 50, locale == "ar") : "";
                bool firstMissing = first.HasValue && first.Value < minimum, secondMissing = actual && farm && current.Counts.Green < 50;
                var segments = new List<BuildingTooltipText> { new BuildingTooltipText(firstText, firstMissing ? 0xFFFF5555u : 0xFFEFF3C6u) };
                if (farm) segments.Add(new BuildingTooltipText(secondText, secondMissing ? 0xFFFF5555u : 0xFFEFF3C6u));
                lines = Array.AsReadOnly(new[] { new BuildingTooltipLine(segments.ToArray()) });
                lastMapper = mapper; lastActual = actual; lastCounts = counts; lastLocale = locale;
                return lines;
            }
            catch (Exception error) { Fail(error); return Array.Empty<BuildingTooltipLine>(); }
        }
        private static void Fail(Exception error)
        {
            if (Interlocked.Exchange(ref failed, 1) == 0)
                Shared.DebugLogHelper.LogError(log, "GROUND_TOOLTIP_DISABLED: " + error);
        }
    }
}
