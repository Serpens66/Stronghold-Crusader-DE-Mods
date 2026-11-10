using APIShared;
using APIShared.GameModes;
using APIShared.Presentation;
using APIShared.Simulation;
using BepInEx.Logging;
using CrusaderDE;
using SHCDESE.API;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;

namespace BugfixesAndQoL
{
    // Process-lived through the APIShared registrations. Only the active schedule belongs to a mission.
    internal sealed class LocalAutosaveRuntime
    {
        private const string RegistrationId = "local-autosave";
        private const string FileName = "Autosave - Singleplayer.sav";
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly IMissionLifecycleCapability missions;
        private readonly IMissionPlayStateReadinessCapability readiness;
        private IngameMenuActionRegistration menu;
        private Launch launch;
        private Schedule schedule;
        private volatile bool enabled;

        private LocalAutosaveRuntime(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log;
            this.settings = settings;
            var client = ApiShared.ForMod(BugfixesAndQoLPlugin.PluginGuid);
            if (!client.TryGetMissionLifecycle(out missions, out var diagnostic) ||
                !client.TryGetMissionPlayStateReadiness(out readiness, out diagnostic) ||
                !missions.TryRegisterObserver(RegistrationId, Started, Ended, null, out diagnostic) ||
                !readiness.TryRegisterObserver(RegistrationId, Ready, out diagnostic))
                throw new InvalidOperationException(diagnostic?.Reason);

            if (!SimulationTickEvents.TryRegister(BugfixesAndQoLPlugin.PluginGuid, RegistrationId,
                OnTick, out string reason))
                throw new InvalidOperationException(reason);

            if (!IngameMenuActions.TryRegister(BugfixesAndQoLPlugin.PluginGuid, RegistrationId,
                MenuState, Cycle, new IngameMenuActionOptions(IngameMenuRow.Save, IngameMenuSlot.Left),
                out menu, out reason) &&
                !IngameMenuActions.TryRegister(BugfixesAndQoLPlugin.PluginGuid, RegistrationId,
                    MenuState, Cycle, new IngameMenuActionOptions(), out menu, out reason))
                throw new InvalidOperationException(reason);

            settings.SettingChanged += SettingsChanged;
            SettingsChanged(null);
        }

        internal static void Initialize(ManualLogSource log, BugfixesAndQoLViewModel settings) =>
            new LocalAutosaveRuntime(log, settings);

        private static bool IsLocalGame(MissionContext context) =>
            context != null && !context.IsEditor &&
            !context.Mode.IsRealMultiplayer && context.Mode.Kind != GameModeKind.Unknown &&
            context.Mode.Kind != GameModeKind.Tutorial;

        private void SettingsChanged(string property)
        {
            if (property == null || property == nameof(settings.EnableClientFeatures))
            {
                enabled = settings.EnableClientFeatures;
                menu?.Invalidate();
            }
        }

        private void Started(MissionLifecycleNotification notification)
        {
            var context = notification.Context;
            var previous = Volatile.Read(ref launch);
            if (!IsLocalGame(context))
            {
                Volatile.Write(ref launch, null);
                return;
            }

            int minutes = 0;
            if (!context.IsSave)
            {
                minutes = context.IsRestart
                    ? (previous != null && context.RestartedFromSessionId == previous.SessionId ? previous.Minutes : 0)
                    : SkirmishGameOptionsRuntime.GetLocalAutosaveMinutes(context);
            }
            Volatile.Write(ref launch, new Launch(context.SessionId, minutes));
        }

        private void Ended(MissionLifecycleNotification notification)
        {
            var current = Volatile.Read(ref schedule);
            if (current?.SessionId == notification.Context.SessionId)
                Interlocked.CompareExchange(ref schedule, null, current);
            menu?.Invalidate();
        }

        private void Ready(MissionPlayStateReadyNotification notification)
        {
            var context = notification.Context;
            if (!IsLocalGame(context) || missions.Current?.SessionId != context.SessionId)
                return;
            var initial = Volatile.Read(ref launch);
            Volatile.Write(ref schedule, new Schedule(context.SessionId,
                initial?.SessionId == context.SessionId ? initial.Minutes : 0));
            menu?.Invalidate();
        }

        private Schedule CurrentSchedule()
        {
            var current = Volatile.Read(ref schedule);
            return current != null && enabled && readiness.Current?.SessionId == current.SessionId
                ? current : null;
        }

        private IngameMenuActionState MenuState()
        {
            var current = CurrentSchedule();
            if (current == null || Director.instance == null || !Director.instance.SimRunning)
                return null;
            string interval = current.Minutes == 0
                ? SerpLocalization.Get("BugfixesAndQoL.LocalAutosaveOff")
                : SerpLocalization.Get("BugfixesAndQoL.LocalAutosaveMinutes").Replace("{Minutes}", current.Minutes.ToString());
            return new IngameMenuActionState(
                SerpLocalization.Get("BugfixesAndQoL.LocalAutosave").Replace("{Interval}", interval),
                true, true, SerpLocalization.Get("BugfixesAndQoL.LocalAutosaveHelp"));
        }

        private void Cycle()
        {
            var current = CurrentSchedule();
            if (current == null || Director.instance == null || !Director.instance.SimRunning)
                return;
            int minutes = SkirmishGameOptionsRuntime.NextAutosaveMinutes(current.Minutes);
            Interlocked.CompareExchange(ref schedule, new Schedule(current.SessionId, minutes), current);
            menu.Invalidate();
        }

        private void OnTick(int tick)
        {
            var current = Volatile.Read(ref schedule);
            if (current == null || current.Minutes == 0 || !enabled ||
                Stopwatch.GetTimestamp() < Interlocked.Read(ref current.Due) ||
                Interlocked.CompareExchange(ref current.Queued, 1, 0) != 0)
                return;
            if (!Shared.UnityMainThreadDispatch.TryEnqueue(() => TrySave(current)))
                DelayRetry(current);
        }

        private void TrySave(Schedule requested)
        {
            bool resetInterval = false;
            try
            {
                if (!ReferenceEquals(CurrentSchedule(), requested) || !CanSave())
                    return;
                resetInterval = WriteSave();
            }
            catch (Exception error)
            {
                resetInterval = true;
                Shared.DebugLogHelper.LogErrorLimited(log, "local-autosave/write", "Local autosave failed: " + error);
            }
            finally
            {
                if (resetInterval)
                {
                    Interlocked.Exchange(ref requested.Due, Schedule.Deadline(requested.Minutes));
                    Volatile.Write(ref requested.Queued, 0);
                }
                else
                    DelayRetry(requested);
            }
        }

        private static void DelayRetry(Schedule requested)
        {
            Interlocked.Exchange(ref requested.Due, Stopwatch.GetTimestamp() + Stopwatch.Frequency);
            Volatile.Write(ref requested.Queued, 0);
        }

        private static bool CanSave()
        {
            if (!MainViewModel.viewModelLoaded || Director.instance == null || !Director.instance.SimRunning ||
                Director.instance.Paused || CameraControls2D.instance == null ||
                PerfectPixelWithZoom.instance == null || GameMap.instance == null)
                return false;
            var view = MainViewModel.Instance;
            return !view.Show_HUD_Briefing && !view.Show_HUD_MissionOver &&
                !GamePlayerManagerAPI.Instance.IsLocalPaused();
        }

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

        private sealed class Schedule
        {
            internal readonly long SessionId;
            internal readonly int Minutes;
            internal long Due;
            internal int Queued;

            internal Schedule(long sessionId, int minutes)
            {
                SessionId = sessionId;
                Minutes = minutes;
                Due = Deadline(minutes);
            }

            internal static long Deadline(int minutes) => Stopwatch.GetTimestamp() + minutes * 60L * Stopwatch.Frequency;
        }
    }
}
