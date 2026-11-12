using APIShared;
using APIShared.GameModes;
using APIShared.Presentation;
using BepInEx.Logging;
using CrusaderDE;
using SHCDESE.API;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace BugfixesAndQoL
{
    // The process-lived APIShared registrations root this runtime after startup cleanup.
    // Mutable scheduling/menu state is owned exclusively by the validated Unity thread.
    internal sealed class LocalAutosaveRuntime
    {
        private const string RegistrationId = "local-autosave";
        private const string FileName = "Autosave - Singleplayer.sav";
        private static readonly TimeSpan EditDelay = TimeSpan.FromSeconds(15);
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly IMissionLifecycleCapability missions;
        private readonly IMissionPlayStateReadinessCapability readiness;
        private readonly UiWorkClient ui;
        private readonly IUiFrameWorkRegistration frameWork, pendingWork;
        private IngameMenuActionRegistration menu;
        private MainViewModel observedMain;
        private Launch launch;
        private LocalAutosaveSchedule schedule;
        private readonly LocalAutosavePendingChoice pending = new LocalAutosavePendingChoice();
        private bool initialized;
        private volatile bool enabled;

        private LocalAutosaveRuntime(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log;
            this.settings = settings;
            var client = ApiShared.ForMod(BugfixesAndQoLPlugin.PluginGuid);
            ui = client.Ui;
            if (!client.TryGetMissionLifecycle(out missions, out var diagnostic) ||
                !client.TryGetMissionPlayStateReadiness(out readiness, out diagnostic))
                throw new InvalidOperationException(diagnostic?.Reason);
            if (!ui.TryRegisterBeforeRender(RegistrationId, OnBeforeRender, out frameWork, out string reason) ||
                !ui.TryRegisterBeforeRender(RegistrationId + "-edit", ApplyPendingWhenIdle, out pendingWork, out reason))
                throw new InvalidOperationException(reason);

            if (!IngameMenuActions.TryRegisterContent(BugfixesAndQoLPlugin.PluginGuid, RegistrationId,
                MenuState, CreateMenuControl, new IngameMenuActionOptions(IngameMenuRow.Save, IngameMenuSlot.Left),
                out menu, out reason) &&
                !IngameMenuActions.TryRegisterContent(BugfixesAndQoLPlugin.PluginGuid, RegistrationId,
                    MenuState, CreateMenuControl, new IngameMenuActionOptions(), out menu, out reason))
                throw new InvalidOperationException(reason);

            // Observers may synchronously replay; callbacks retain state but remain dormant
            // until all required process-lifetime registrations have succeeded.
            if (!missions.TryRegisterObserver(RegistrationId, Started, Ended, null, out diagnostic) ||
                !readiness.TryRegisterObserver(RegistrationId, Ready, out diagnostic))
                throw new InvalidOperationException(diagnostic?.Reason);
            settings.SettingChanged += SettingsChanged;
            initialized = true;
            ApplySettings();
        }

        internal static void Initialize(ManualLogSource log, BugfixesAndQoLViewModel settings) =>
            new LocalAutosaveRuntime(log, settings);

        private static bool IsLocalGame(MissionContext context) =>
            context != null && !context.IsEditor && !context.Mode.IsRealMultiplayer &&
            context.Mode.Kind != GameModeKind.Unknown && context.Mode.Kind != GameModeKind.Tutorial;

        private void SettingsChanged(string property)
        {
            if (property != null && property != nameof(settings.EnableClientFeatures) &&
                property != nameof(settings.EnableLocalAutosave)) return;
            bool value = settings.EnableClientFeatures && settings.EnableLocalAutosave;
            if (!value) enabled = false;
            if (Shared.UnityMainThreadDispatch.IsMainThread) ApplySettings(value);
            else if (!ui.TryPost(() => ApplySettings(value)))
                Shared.DebugLogHelper.LogErrorLimited(log, "local-autosave/settings-dispatch",
                    "Local autosave settings could not be dispatched to Unity.");
        }

        private void ApplySettings() => ApplySettings(settings.EnableClientFeatures && settings.EnableLocalAutosave);

        private void ApplySettings(bool value)
        {
            enabled = value;
            schedule?.SetEnabled(enabled, Stopwatch.GetTimestamp());
            if (!enabled) CancelPendingEdit();
            UpdateDemand();
            menu?.Invalidate();
        }

        private void Started(MissionLifecycleNotification notification)
        {
            var context = notification.Context;
            var previous = launch;
            schedule?.Cancel(Stopwatch.GetTimestamp());
            schedule = null;
            CancelPendingEdit();
            if (!IsLocalGame(context)) launch = null;
            else
            {
                int minutes = 0;
                if (!context.IsSave)
                    minutes = context.IsRestart
                        ? (previous != null && context.RestartedFromSessionId == previous.SessionId ? previous.Minutes : 0)
                        : SkirmishGameOptionsRuntime.GetLocalAutosaveMinutes(context);
                launch = new Launch(context.SessionId, minutes);
            }
            UpdateDemand();
        }

        private void Ended(MissionLifecycleNotification notification)
        {
            if (schedule?.SessionId == notification.Context.SessionId)
            {
                schedule.Cancel(Stopwatch.GetTimestamp());
                schedule = null;
                CancelPendingEdit();
            }
            UpdateDemand();
            menu?.Invalidate();
        }

        private void Ready(MissionPlayStateReadyNotification notification)
        {
            var context = notification.Context;
            if (!IsLocalGame(context) || missions.Current?.SessionId != context.SessionId ||
                schedule?.SessionId == context.SessionId) return;
            schedule?.Cancel(Stopwatch.GetTimestamp());
            schedule = new LocalAutosaveSchedule(context.SessionId,
                launch?.SessionId == context.SessionId ? launch.Minutes : 0,
                Stopwatch.GetTimestamp(), Stopwatch.Frequency, enabled);
            UpdateDemand();
            menu?.Invalidate();
        }

        private LocalAutosaveSchedule CurrentSchedule() =>
            initialized && enabled && schedule != null && readiness.Current?.SessionId == schedule.SessionId &&
                missions.Current?.SessionId == schedule.SessionId ? schedule : null;

        private void UpdateDemand() => frameWork.SetContinuous(CurrentSchedule()?.Minutes > 0);

        private IngameMenuActionState MenuState()
        {
            var current = CurrentSchedule();
            if (current == null || Director.instance == null || !Director.instance.SimRunning) return null;
            return new IngameMenuActionState(SerpLocalization.Get("BugfixesAndQoL.LocalAutosave")
                .Replace("{Interval}", current.Minutes == 0 ? SerpLocalization.Get("BugfixesAndQoL.LocalAutosaveOff") :
                    SerpLocalization.Get("BugfixesAndQoL.LocalAutosaveMinutes").Replace("{Minutes}", current.Minutes.ToString())),
                true, true, SerpLocalization.Get("BugfixesAndQoL.LocalAutosaveHelp"));
        }

        private Noesis.Control CreateMenuControl(IngameMenuContentContext context)
        {
            ObserveMain();
            return new LocalAutosaveMenuControl(context, GetEditorMinutes, SetPendingMinutes, TouchPendingEdit, CancelPendingEdit);
        }

        private void ObserveMain()
        {
            if (!MainViewModel.viewModelLoaded) return;
            var main = MainViewModel.Instance;
            if (ReferenceEquals(main, observedMain)) return;
            if (observedMain != null) observedMain.PropertyChanged -= MainChanged;
            observedMain = main;
            if (main != null) main.PropertyChanged += MainChanged;
        }

        private void MainChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName != nameof(MainViewModel.Show_HUD_IngameMenu) ||
                !ReferenceEquals(sender, observedMain)) return;
            if (Shared.UnityMainThreadDispatch.IsMainThread) ApplyPendingIfMenuClosed();
            else if (!ui.TryPost(ApplyPendingIfMenuClosed))
                Shared.DebugLogHelper.LogErrorLimited(log, "local-autosave/menu-dispatch",
                    "Local autosave menu close could not be dispatched to Unity.");
        }

        private void ApplyPendingIfMenuClosed()
        { if (observedMain != null && !observedMain.Show_HUD_IngameMenu) ApplyPending(); }

        private int GetEditorMinutes() => pending.EditorMinutes(CurrentSchedule());

        private void SetPendingMinutes(int minutes)
        {
            var current = CurrentSchedule();
            if (current == null || minutes < 0 || minutes > LocalAutosaveInputPolicy.MaximumMinutes) return;
            pending.Set(current, minutes, Stopwatch.GetTimestamp());
            pendingWork.RequestAfter(EditDelay);
        }

        private void TouchPendingEdit()
        {
            // A valid numeric prefix must never be committed after the user made the
            // visible draft invalid (for example 14 -> 1441).
            pending.Invalidate(Stopwatch.GetTimestamp());
            pendingWork.CancelPending();
        }

        private void CancelPendingEdit()
        {
            pending.Clear();
            pendingWork.CancelPending();
        }

        private void ApplyPendingWhenIdle()
        {
            long remaining = pending.RemainingTicks(Stopwatch.GetTimestamp(), Stopwatch.Frequency);
            if (remaining < 0) return;
            if (remaining > 0)
            {
                pendingWork.RequestAfter(TimeSpan.FromSeconds(remaining / (double)Stopwatch.Frequency));
                return;
            }
            ApplyPending();
        }

        private void ApplyPending()
        {
            var current = CurrentSchedule();
            if (pending.TryTake(current, out int minutes))
            {
                current.SetMinutes(minutes, Stopwatch.GetTimestamp());
                UpdateDemand();
            }
            CancelPendingEdit();
        }

        private void OnBeforeRender()
        {
            var current = CurrentSchedule();
            if (current == null || current.Minutes == 0) return;
            bool active = IsPlaying();
            long now = Stopwatch.GetTimestamp();
            current.Sample(now, active);
            if (active && current.TryQueue(now, out long generation)) QueueSave(current, generation);
        }

        private void QueueSave(LocalAutosaveSchedule requested, long generation)
        {
            if (!ui.TryPost(() => TrySave(requested, generation)))
                requested.Complete(generation, LocalAutosaveAttemptResult.Deferred, Stopwatch.GetTimestamp());
        }

        private void TrySave(LocalAutosaveSchedule requested, long generation)
        {
            long now = Stopwatch.GetTimestamp();
            if (!ReferenceEquals(CurrentSchedule(), requested) || !requested.IsCurrentAttempt(generation, now)) return;
            var result = LocalAutosaveAttemptResult.Deferred;
            try
            {
                if (!CanSave()) return;
                requested.Sample(now, false);
                result = WriteSave() ? LocalAutosaveAttemptResult.Saved : LocalAutosaveAttemptResult.Deferred;
            }
            catch (Exception error)
            {
                result = LocalAutosaveAttemptResult.Failed;
                Shared.DebugLogHelper.LogErrorLimited(log, "local-autosave/write", "Local autosave failed: " + error);
            }
            finally { requested.Complete(generation, result, Stopwatch.GetTimestamp()); }
        }

        private static bool IsPlaying()
        {
            if (!MainViewModel.viewModelLoaded || Director.instance == null || !Director.instance.SimRunning ||
                Director.instance.Paused) return false;
            var view = MainViewModel.Instance;
            return !view.Show_HUD_IngameMenu && !view.Show_HUD_Briefing && !view.Show_HUD_MissionOver &&
                !GamePlayerManagerAPI.Instance.IsLocalPaused();
        }

        private static bool CanSave() => IsPlaying() && CameraControls2D.instance != null &&
            PerfectPixelWithZoom.instance != null && GameMap.instance != null;

        private static bool WriteSave()
        {
            float zoom = PerfectPixelWithZoom.instance.getZoom();
            var camera = CameraControls2D.instance.getScreenXYForSaveCentring(
                (int)((Screen.width / 64) / zoom), (int)((Screen.height / 16) / zoom));
            int centreX = -1, centreY = -1;
            GameMap.instance.getRotationCentre(ref centreX, ref centreY);
            return WriteSaveFile(ConfigSettings.GetSavesPath(),
                path => EngineInterface.SaveSaveGame(path, camera.x, camera.y, centreX, centreY));
        }

        internal static bool WriteSaveFile(string directory, Func<string, bool> write)
        {
            string destination = Path.Combine(directory, FileName);
            // Keep partially written files outside Vanilla's top-level *.sav listing, on the same volume.
            string staging = Path.Combine(directory, ".serps-autosave-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            string temporary = Path.Combine(staging, FileName);
            try
            {
                // Vanilla also returns false when the simulation never became safe to save.
                bool written = write(temporary);
                if (!written && !File.Exists(temporary))
                    return false;
                if (!written || !HasCompleteSaveFrame(temporary))
                    throw new IOException("The game did not complete the save.");

                if (File.Exists(destination))
                    File.Replace(temporary, destination, null);
                else
                    File.Move(temporary, destination);
                return true;
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
                Directory.Delete(staging);
            }
        }

        private static bool HasCompleteSaveFrame(string path)
        {
            // The native writer does not propagate short writes. Validate its declared
            // payload and the boundary to any archive appended by the Script Extender.
            const uint saveMagic = 0xFFFFFFFEu;
            const int preambleBlocks = 6;
            const int directorySize = 4036;
            const int sectionCapacity = 200;
            using (var reader = new BinaryReader(File.OpenRead(path)))
            {
                Stream stream = reader.BaseStream;
                if (stream.Length < sizeof(uint) || reader.ReadUInt32() != saveMagic)
                    return false;

                uint blockSize = 0;
                for (int block = 0; block < preambleBlocks; block++)
                {
                    blockSize = reader.ReadUInt32();
                    stream.Position += blockSize;
                    if (stream.Position > stream.Length)
                        return false;
                }

                if (blockSize != 0)
                {
                    uint restartSize = reader.ReadUInt32();
                    stream.Position += restartSize;
                    if (restartSize != 0 && (stream.Length - stream.Position < sizeof(uint) || reader.ReadUInt32() != 0))
                        return false;
                }

                long directory = stream.Position;
                if (stream.Length - directory < directorySize || reader.ReadInt32() != directorySize)
                    return false;
                uint payloadSize = reader.ReadUInt32();
                uint sectionCount = reader.ReadUInt32();
                if (payloadSize == 0 || sectionCount == 0 || sectionCount > sectionCapacity)
                    return false;

                long nativeEnd = directory + directorySize + payloadSize;
                return nativeEnd == stream.Length || (nativeEnd < stream.Length && HasAppendedArchive(reader, nativeEnd));
            }
        }

        private static bool HasAppendedArchive(BinaryReader reader, long nativeEnd)
        {
            const uint endOfCentralDirectory = 0x06054B50u;
            const int footerSize = 22;
            const int directoryLengthOffset = 12;
            const int directoryPositionOffset = 16;
            const int commentLengthOffset = 20;
            Stream stream = reader.BaseStream;
            // A complete appended ZIP starts exactly after the native payload. A ZIP
            // appended to a shortened payload must not mask missing game data.
            int tailSize = (int)Math.Min(stream.Length - nativeEnd, footerSize + ushort.MaxValue);
            stream.Position = stream.Length - tailSize;
            byte[] tail = reader.ReadBytes(tailSize);
            for (int offset = tail.Length - footerSize; offset >= 0; offset--)
            {
                if (BitConverter.ToUInt32(tail, offset) != endOfCentralDirectory ||
                    BitConverter.ToUInt16(tail, offset + commentLengthOffset) != tail.Length - offset - footerSize)
                    continue;
                uint size = BitConverter.ToUInt32(tail, offset + directoryLengthOffset);
                uint position = BitConverter.ToUInt32(tail, offset + directoryPositionOffset);
                if (stream.Length - tail.Length + offset - size - position == nativeEnd)
                    return true;
            }
            return false;
        }

        private sealed class Launch
        {
            internal readonly long SessionId;
            internal readonly int Minutes;
            internal Launch(long sessionId, int minutes) { SessionId = sessionId; Minutes = minutes; }
        }
    }
}
