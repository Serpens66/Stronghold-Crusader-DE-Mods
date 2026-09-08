using BepInEx.Logging;
using MessagePack;
using MessagePack.Formatters;
using SHCDESE.API;
using SHCDESE.API.Components.SaveData;
using System;
using System.Threading;

namespace BugfixesAndQoL
{
    public enum TrailCustomizationLaunchOriginKind
    {
        None,
        CustomizedCustomTrail,
        CustomizedCoopTrail,
    }

    /// <summary>Optional reflection surface used by Shared game-mode classification.</summary>
    public static class TrailCustomizationLaunchOriginApi
    {
        // Version 1 intentionally advertises only Custom/Coop origins. Built-in Trail
        // and Sands-of-Time origin tracking remains CustomCustomTrail's responsibility.
        private const int CurrentApiVersion = 1;
        private const string SaveDataIdentifier = "BugfixesAndQoL-TrailCustomizationOrigin";
        private static readonly object Sync = new object();
        private static int initialized;
        private static ManualLogSource log;
        private static TrailCustomizationLaunchOriginKind origin;
        private static int trailId = -1;
        private static int missionId = -1;
        private static bool restoredFromSave;
        private static bool launchPending;

        public static int ApiVersion => CurrentApiVersion;
        public static TrailCustomizationLaunchOriginKind Origin { get { lock (Sync) return origin; } }
        public static int TrailType => -1;
        public static int TrailId { get { lock (Sync) return trailId; } }
        public static int MissionId { get { lock (Sync) return missionId; } }
        public static bool RestoredFromSave { get { lock (Sync) return restoredFromSave; } }
        public static bool LaunchPending { get { lock (Sync) return launchPending; } }

        internal static void Initialize(ManualLogSource logger)
        {
            log = logger;
            if (Interlocked.Exchange(ref initialized, 1) != 0)
                return;
            if (!ModSaveDataAPI.Instance.RegisterModDataHandler(
                    SaveDataIdentifier,
                    SaveState,
                    LoadState,
                    OnMapUnloaded))
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    "Could not register Trail customization launch-origin data; restored saves will fail closed.");
            }
        }

        internal static void SetCustomizedCustomTrail(int selectedTrailId, int selectedMissionId)
        {
            if (selectedTrailId < 90 || selectedTrailId > 92 || selectedMissionId < 1)
            {
                Clear();
                return;
            }
            Set(TrailCustomizationLaunchOriginKind.CustomizedCustomTrail, selectedTrailId, selectedMissionId, false);
        }

        internal static void SetCustomizedCoopTrail(int zeroBasedTrailId, int selectedMissionId)
        {
            if (zeroBasedTrailId < 0 || zeroBasedTrailId > 3 || selectedMissionId < 1 || selectedMissionId > 10)
            {
                Clear();
                return;
            }
            Set(TrailCustomizationLaunchOriginKind.CustomizedCoopTrail, zeroBasedTrailId, selectedMissionId, false);
        }

        internal static void Clear()
        {
            TrailCustomizationLaunchOriginKind previous;
            lock (Sync)
            {
                previous = origin;
                origin = TrailCustomizationLaunchOriginKind.None;
                trailId = -1;
                missionId = -1;
                restoredFromSave = false;
                launchPending = false;
            }
            if (previous != TrailCustomizationLaunchOriginKind.None)
                Shared.DebugLogHelper.LogInfo(log, $"Cleared Trail customization origin: previous={previous}.");
        }

        internal static void MarkMapStarted()
        {
            lock (Sync)
                launchPending = false;
        }

        internal static void MarkRestartPending()
        {
            lock (Sync)
            {
                if (origin != TrailCustomizationLaunchOriginKind.None)
                    launchPending = true;
            }
        }

        private static void OnMapUnloaded()
        {
            lock (Sync)
            {
                if (launchPending)
                    return;
            }
            Clear();
        }

        private static byte[] SaveState(SaveContext context)
        {
            if (context == null || !context.IsSaveFile || context.IsMapEditorSave)
                return null;
            lock (Sync)
            {
                if (origin == TrailCustomizationLaunchOriginKind.None)
                    return null;
                return MessagePackSerializer.Serialize(new TrailCustomizationOriginSaveData
                {
                    Version = CurrentApiVersion,
                    Origin = (int)origin,
                    TrailId = trailId,
                    MissionId = missionId,
                });
            }
        }

        private static void LoadState(byte[] bytes, LoadContext context)
        {
            Clear();
            if (context == null || !context.IsSaveFile || bytes == null || bytes.Length == 0)
                return;
            try
            {
                TrailCustomizationOriginSaveData data =
                    MessagePackSerializer.Deserialize<TrailCustomizationOriginSaveData>(bytes);
                if (!IsValid(data))
                {
                    Shared.DebugLogHelper.LogWarning(log, "Ignored invalid Trail customization origin save data.");
                    return;
                }
                Set((TrailCustomizationLaunchOriginKind)data.Origin, data.TrailId, data.MissionId, true);
            }
            catch (Exception exception)
            {
                Clear();
                Shared.DebugLogHelper.LogWarning(
                    log,
                    "Ignored unreadable Trail customization origin save data: " + exception.Message);
            }
        }

        private static bool IsValid(TrailCustomizationOriginSaveData data) =>
            data != null && data.Version == CurrentApiVersion &&
            ((data.Origin == (int)TrailCustomizationLaunchOriginKind.CustomizedCustomTrail &&
              data.TrailId >= 90 && data.TrailId <= 92 && data.MissionId >= 1) ||
             (data.Origin == (int)TrailCustomizationLaunchOriginKind.CustomizedCoopTrail &&
              data.TrailId >= 0 && data.TrailId <= 3 && data.MissionId >= 1 && data.MissionId <= 10));

        private static void Set(
            TrailCustomizationLaunchOriginKind value,
            int selectedTrailId,
            int selectedMissionId,
            bool restored)
        {
            lock (Sync)
            {
                origin = value;
                trailId = selectedTrailId;
                missionId = selectedMissionId;
                restoredFromSave = restored;
                launchPending = true;
            }
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Set Trail customization origin: origin={value}, trailId={selectedTrailId}, " +
                $"missionId={selectedMissionId}, restored={restored}.");
        }

        [MessagePackObject]
        [MessagePackFormatter(typeof(TrailCustomizationOriginSaveDataFormatter))]
        public sealed class TrailCustomizationOriginSaveData
        {
            [Key(0)] public int Version { get; set; }
            [Key(1)] public int Origin { get; set; }
            [Key(2)] public int TrailId { get; set; }
            [Key(3)] public int MissionId { get; set; }
        }

        public sealed class TrailCustomizationOriginSaveDataFormatter :
            IMessagePackFormatter<TrailCustomizationOriginSaveData>
        {
            public void Serialize(
                ref MessagePackWriter writer,
                TrailCustomizationOriginSaveData value,
                MessagePackSerializerOptions options)
            {
                if (value == null)
                {
                    writer.WriteNil();
                    return;
                }
                writer.WriteArrayHeader(4);
                writer.Write(value.Version);
                writer.Write(value.Origin);
                writer.Write(value.TrailId);
                writer.Write(value.MissionId);
            }

            public TrailCustomizationOriginSaveData Deserialize(
                ref MessagePackReader reader,
                MessagePackSerializerOptions options)
            {
                if (reader.TryReadNil())
                    return null;
                int count = reader.ReadArrayHeader();
                var value = new TrailCustomizationOriginSaveData();
                for (int index = 0; index < count; index++)
                {
                    switch (index)
                    {
                        case 0: value.Version = reader.ReadInt32(); break;
                        case 1: value.Origin = reader.ReadInt32(); break;
                        case 2: value.TrailId = reader.ReadInt32(); break;
                        case 3: value.MissionId = reader.ReadInt32(); break;
                        default: reader.Skip(); break;
                    }
                }
                return value;
            }
        }
    }
}
