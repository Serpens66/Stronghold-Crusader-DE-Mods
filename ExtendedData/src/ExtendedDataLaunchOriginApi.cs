using BepInEx.Logging;
using MessagePack;
using MessagePack.Formatters;
using Shared;
using SHCDESE.API;
using SHCDESE.API.Components.SaveData;
using System;
using System.Threading;

namespace ExtendedData
{
    public enum ExtendedDataLaunchOriginKind
    {
        None,
        CustomizedCustomTrail,
        CustomizedCoopTrail,
        CustomizedVanillaTrail,
        CustomizedSandsOfTime,
    }

    /// <summary>Consumer-owned launch-origin state and save-data protocol.</summary>
    public static partial class ExtendedDataLaunchOriginApi
    {
        private const int CurrentApiVersion = 2;
        private const int LegacyApiVersion = 1;
        private const int FirstCustomTrailId = 90;
        private const int LastCustomTrailId = 92;
        private const int FirstCoopTrailId = 0;
        private const int LastCoopTrailId = 3;
        private const int FirstMissionId = 1;
        private const int LastCoopMissionId = 10;
        // This optional API is compiled independently by its save-data tests, so it keeps
        // named protocol boundaries instead of depending on GameModeHelper's internal enum.
        internal const int FirstVanillaTrailType = 0;
        internal const int LastVanillaTrailType = 2;
        internal const int FirstSandsOfTimeTrailType = 11;
        internal const int LastSandsOfTimeTrailType = 18;
        private const string SaveDataIdentifier = "ExtendedData-LaunchOrigin";
        private static readonly object Sync = new object();

        private static int initialized;
        private static ManualLogSource log;
        private static ExtendedDataLaunchOriginKind origin;
        private static int trailType = -1;
        private static int trailId = -1;
        private static int missionId = -1;
        private static bool restoredFromSave;
        private static bool launchPending;
        private static long? missionSessionId;

        public static int ApiVersion => CurrentApiVersion;
        public static ExtendedDataLaunchOriginKind Origin { get { lock (Sync) return origin; } }
        public static int TrailType { get { lock (Sync) return trailType; } }
        public static int TrailId { get { lock (Sync) return trailId; } }
        public static int MissionId { get { lock (Sync) return missionId; } }
        public static bool RestoredFromSave { get { lock (Sync) return restoredFromSave; } }
        public static bool LaunchPending { get { lock (Sync) return launchPending; } }

        internal static void Initialize(ManualLogSource logger)
        {
            log = logger;
            if (Interlocked.Exchange(ref initialized, 1) != 0)
                return;

            bool registered = ModSaveDataAPI.Instance.RegisterModDataHandler(
                SaveDataIdentifier,
                SaveState,
                LoadState,
                OnMapUnloaded);
            if (!registered)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/ExtendedDataLaunchOriginApi.cs:71", "Could not register persistent Custom Trail launch-origin data; restored saves will fail closed.");
            }
        }

        internal static void SetCustomizedCustomTrail(int selectedTrailId, int selectedMissionId)
        {
            if (selectedTrailId < FirstCustomTrailId || selectedTrailId > LastCustomTrailId ||
                selectedMissionId < FirstMissionId)
            {
                Clear();
                return;
            }
            Set(ExtendedDataLaunchOriginKind.CustomizedCustomTrail,
                trailTypeValue: -1,
                selectedTrailId,
                selectedMissionId,
                restored: false);
        }

        internal static void SetCustomizedCoopTrail(int zeroBasedTrailId, int selectedMissionId)
        {
            if (zeroBasedTrailId < FirstCoopTrailId || zeroBasedTrailId > LastCoopTrailId ||
                selectedMissionId < FirstMissionId || selectedMissionId > LastCoopMissionId)
            {
                Clear();
                return;
            }
            Set(ExtendedDataLaunchOriginKind.CustomizedCoopTrail,
                trailTypeValue: -1,
                zeroBasedTrailId,
                selectedMissionId,
                restored: false);
        }

        internal static void SetCustomizedVanillaTrail(int trailTypeValue, int selectedTrailId, int selectedMissionId)
        {
            SetCustomizedBuiltInTrail(
                ExtendedDataLaunchOriginKind.CustomizedVanillaTrail,
                trailTypeValue,
                selectedTrailId,
                selectedMissionId,
                minimumTrailType: FirstVanillaTrailType,
                maximumTrailType: LastVanillaTrailType);
        }

        internal static void SetCustomizedSandsOfTime(int trailTypeValue, int selectedTrailId, int selectedMissionId)
        {
            SetCustomizedBuiltInTrail(
                ExtendedDataLaunchOriginKind.CustomizedSandsOfTime,
                trailTypeValue,
                selectedTrailId,
                selectedMissionId,
                minimumTrailType: FirstSandsOfTimeTrailType,
                maximumTrailType: LastSandsOfTimeTrailType);
        }

        private static void SetCustomizedBuiltInTrail(
            ExtendedDataLaunchOriginKind originValue,
            int trailTypeValue,
            int selectedTrailId,
            int selectedMissionId,
            int minimumTrailType,
            int maximumTrailType)
        {
            if (trailTypeValue < minimumTrailType || trailTypeValue > maximumTrailType ||
                selectedTrailId < 0 || selectedMissionId < 0)
            {
                Clear();
                return;
            }
            Set(originValue, trailTypeValue, selectedTrailId, selectedMissionId, restored: false);
        }

        internal static void Clear()
        {
            ExtendedDataLaunchOriginKind previous;
            lock (Sync)
            {
                previous = origin;
                origin = ExtendedDataLaunchOriginKind.None;
                trailType = -1;
                trailId = -1;
                missionId = -1;
                restoredFromSave = false;
                launchPending = false;
                missionSessionId = null;
            }
            if (previous != ExtendedDataLaunchOriginKind.None)
                {
                    if (Shared.DebugLogHelper.DiagnosticsEnabled)
                        Shared.DebugLogHelper.LogDebugLimited(log, "ExtendedData/src/ExtendedDataLaunchOriginApi.cs:160", $"Cleared customized launch origin: previous={previous}.");
                }
        }

        internal static void MarkMapStarted(long? sessionId = null)
        {
            lock (Sync)
            {
                launchPending = false;
                missionSessionId = sessionId;
            }
        }

        internal static void MarkRestartPending()
        {
            lock (Sync)
            {
                if (origin != ExtendedDataLaunchOriginKind.None)
                    launchPending = true;
            }
        }

        internal static void PrepareCoopLaunch(int zeroBasedTrailId, int selectedMissionId, bool restarting)
        {
            if (restarting)
                MarkRestartPending();
            else
                SetCustomizedCoopTrail(zeroBasedTrailId, selectedMissionId);
        }

        internal static void OnMissionEnded(long sessionId, APIShared.MissionEndReason reason,
            bool hasStarted, long? replacementSessionId, bool restarting)
        {
            lock (Sync)
            {
                if (reason != APIShared.MissionEndReason.ApplicationExit &&
                    missionSessionId.HasValue && missionSessionId != sessionId)
                    return; // A stale End cannot clear a newer session's origin.
                if (reason == APIShared.MissionEndReason.Replaced && replacementSessionId.HasValue)
                {
                    // Set() has already replaced the old origin for an ordinary new
                    // launch. A restart instead transfers its existing ownership.
                    if (restarting || !missionSessionId.HasValue && launchPending)
                    {
                        missionSessionId = replacementSessionId;
                        launchPending = origin != ExtendedDataLaunchOriginKind.None;
                        return;
                    }
                }
                else if (!missionSessionId.HasValue && launchPending && hasStarted &&
                    (reason == APIShared.MissionEndReason.Unloaded || reason == APIShared.MissionEndReason.SceneChanged))
                    return; // The next launch was prepared before its root load entry.
                Clear(); // Includes an abandoned delayed client attempt, even without Start.
            }
        }

        private static void OnMapUnloaded()
        {
            lock (Sync)
            {
                // Script Extender 1.42 invokes this handler in both unload phases. Keep a
                // frontend/save launch origin until OnStartMap confirms the destination map.
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
                if (origin == ExtendedDataLaunchOriginKind.None)
                    return null;
                return MessagePackSerializer.Serialize(new LaunchOriginSaveData
                {
                    Version = CurrentApiVersion,
                    Origin = (int)origin,
                    TrailType = trailType,
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
                LaunchOriginSaveData data = MessagePackSerializer.Deserialize<LaunchOriginSaveData>(bytes);
                if (!IsValid(data))
                {
                    Shared.DebugLogHelper.LogWarningLimited(log, "ExtendedData/src/ExtendedDataLaunchOriginApi.cs:219", "Ignored invalid Custom Trail launch-origin save data.");
                    return;
                }
                Set((ExtendedDataLaunchOriginKind)data.Origin,
                    data.TrailType,
                    data.TrailId,
                    data.MissionId,
                    restored: true);
                if (Shared.DebugLogHelper.DiagnosticsEnabled)
                    Shared.DebugLogHelper.LogDebugLimited(log, "ExtendedData/src/ExtendedDataLaunchOriginApi.cs:227", $"Restored customized launch origin from save: origin={data.Origin}, trailId={data.TrailId}, missionId={data.MissionId}.");
            }
            catch (Exception exception)
            {
                Clear();
                Shared.DebugLogHelper.LogWarningLimited(log, "ExtendedData/src/ExtendedDataLaunchOriginApi.cs:234", "Ignored unreadable Custom Trail launch-origin save data: " + exception.Message);
            }
        }

        private static bool IsValid(LaunchOriginSaveData data)
        {
            if (data == null ||
                (data.Version != LegacyApiVersion && data.Version != CurrentApiVersion))
                return false;
            if (data.Origin == (int)ExtendedDataLaunchOriginKind.CustomizedCustomTrail)
                return data.MissionId >= FirstMissionId &&
                    data.TrailId >= FirstCustomTrailId && data.TrailId <= LastCustomTrailId;
            if (data.Origin == (int)ExtendedDataLaunchOriginKind.CustomizedCoopTrail)
            {
                return data.MissionId >= FirstMissionId &&
                    data.TrailId >= FirstCoopTrailId && data.TrailId <= LastCoopTrailId &&
                    data.MissionId <= LastCoopMissionId;
            }
            if (data.Version < CurrentApiVersion || data.TrailId < 0 || data.MissionId < 0)
                return false;
            if (data.Origin == (int)ExtendedDataLaunchOriginKind.CustomizedVanillaTrail)
                return data.TrailType >= FirstVanillaTrailType &&
                    data.TrailType <= LastVanillaTrailType;
            return data.Origin == (int)ExtendedDataLaunchOriginKind.CustomizedSandsOfTime &&
                data.TrailType >= FirstSandsOfTimeTrailType &&
                data.TrailType <= LastSandsOfTimeTrailType;
        }

        private static void Set(
            ExtendedDataLaunchOriginKind originValue,
            int trailTypeValue,
            int trailIdValue,
            int missionIdValue,
            bool restored)
        {
            lock (Sync)
            {
                origin = originValue;
                trailType = trailTypeValue;
                trailId = trailIdValue;
                missionId = missionIdValue;
                restoredFromSave = restored;
                launchPending = true;
                missionSessionId = null;
            }
            if (Shared.DebugLogHelper.DiagnosticsEnabled)
                Shared.DebugLogHelper.LogDebugLimited(log, "ExtendedData/src/ExtendedDataLaunchOriginApi.cs:278", $"Set customized launch origin: origin={originValue}, trailType={trailTypeValue}, " +
                    $"trailId={trailIdValue}, missionId={missionIdValue}, restored={restored}.");
        }

        [MessagePackObject]
        [MessagePackFormatter(typeof(LaunchOriginSaveDataFormatter))]
        internal sealed class LaunchOriginSaveData
        {
            [Key(0)] public int Version { get; set; }
            [Key(1)] public int Origin { get; set; }
            [Key(2)] public int TrailType { get; set; }
            [Key(3)] public int TrailId { get; set; }
            [Key(4)] public int MissionId { get; set; }
        }

        internal sealed class LaunchOriginSaveDataFormatter : IMessagePackFormatter<LaunchOriginSaveData>
        {
            private const int FieldCount = 5;

            public void Serialize(
                ref MessagePackWriter writer,
                LaunchOriginSaveData value,
                MessagePackSerializerOptions options)
            {
                if (value == null)
                {
                    writer.WriteNil();
                    return;
                }

                writer.WriteArrayHeader(FieldCount);
                writer.Write(value.Version);
                writer.Write(value.Origin);
                writer.Write(value.TrailType);
                writer.Write(value.TrailId);
                writer.Write(value.MissionId);
            }

            public LaunchOriginSaveData Deserialize(
                ref MessagePackReader reader,
                MessagePackSerializerOptions options)
            {
                if (reader.TryReadNil())
                    return null;

                int fieldCount = reader.ReadArrayHeader();
                var data = new LaunchOriginSaveData();
                for (int index = 0; index < fieldCount; index++)
                {
                    switch (index)
                    {
                        case 0: data.Version = reader.ReadInt32(); break;
                        case 1: data.Origin = reader.ReadInt32(); break;
                        case 2: data.TrailType = reader.ReadInt32(); break;
                        case 3: data.TrailId = reader.ReadInt32(); break;
                        case 4: data.MissionId = reader.ReadInt32(); break;
                        default: reader.Skip(); break;
                    }
                }
                return data;
            }
        }
    }
}
