using BepInEx.Logging;
using MessagePack;
using MessagePack.Formatters;
using Shared;
using SHCDESE.API;
using SHCDESE.API.Components.SaveData;
using System;
using System.Threading;

namespace CustomCustomTrail
{
    public enum CustomCustomTrailLaunchOriginKind
    {
        None,
        CustomizedCustomTrail,
        CustomizedCoopTrail,
    }

    /// <summary>Optional, dependency-free reflection surface for shared game-mode classification.</summary>
    public static class CustomCustomTrailLaunchOriginApi
    {
        private const int CurrentApiVersion = 1;
        private const int FirstCustomTrailId = 90;
        private const int LastCustomTrailId = 92;
        private const int FirstCoopTrailId = 0;
        private const int LastCoopTrailId = 3;
        private const int FirstMissionId = 1;
        private const int LastCoopMissionId = 10;
        private const string SaveDataIdentifier = "CustomCustomTrail-LaunchOrigin";
        private static readonly object Sync = new object();

        private static int initialized;
        private static ManualLogSource log;
        private static CustomCustomTrailLaunchOriginKind origin;
        private static int trailType = -1;
        private static int trailId = -1;
        private static int missionId = -1;
        private static bool restoredFromSave;
        private static bool launchPending;

        public static int ApiVersion => CurrentApiVersion;
        public static CustomCustomTrailLaunchOriginKind Origin { get { lock (Sync) return origin; } }
        public static int TrailType { get { lock (Sync) return trailType; } }
        public static int TrailId { get { lock (Sync) return trailId; } }
        public static int MissionId { get { lock (Sync) return missionId; } }
        public static bool RestoredFromSave { get { lock (Sync) return restoredFromSave; } }

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
                DebugLogHelper.LogError(
                    log,
                    "Could not register persistent Custom Trail launch-origin data; restored saves will fail closed.");
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
            Set(CustomCustomTrailLaunchOriginKind.CustomizedCustomTrail,
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
            Set(CustomCustomTrailLaunchOriginKind.CustomizedCoopTrail,
                trailTypeValue: -1,
                zeroBasedTrailId,
                selectedMissionId,
                restored: false);
        }

        internal static void Clear()
        {
            lock (Sync)
            {
                origin = CustomCustomTrailLaunchOriginKind.None;
                trailType = -1;
                trailId = -1;
                missionId = -1;
                restoredFromSave = false;
                launchPending = false;
            }
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
                if (origin != CustomCustomTrailLaunchOriginKind.None)
                    launchPending = true;
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
                if (origin == CustomCustomTrailLaunchOriginKind.None)
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
                    DebugLogHelper.LogWarning(log, "Ignored invalid Custom Trail launch-origin save data.");
                    return;
                }
                Set((CustomCustomTrailLaunchOriginKind)data.Origin,
                    data.TrailType,
                    data.TrailId,
                    data.MissionId,
                    restored: true);
                DebugLogHelper.LogInfo(
                    log,
                    $"Restored customized launch origin from save: origin={data.Origin}, trailId={data.TrailId}, missionId={data.MissionId}.");
            }
            catch (Exception exception)
            {
                Clear();
                DebugLogHelper.LogWarning(log, "Ignored unreadable Custom Trail launch-origin save data: " + exception.Message);
            }
        }

        private static bool IsValid(LaunchOriginSaveData data)
        {
            if (data == null || data.Version != CurrentApiVersion || data.MissionId < FirstMissionId)
                return false;
            if (data.Origin == (int)CustomCustomTrailLaunchOriginKind.CustomizedCustomTrail)
                return data.TrailId >= FirstCustomTrailId && data.TrailId <= LastCustomTrailId;
            return data.Origin == (int)CustomCustomTrailLaunchOriginKind.CustomizedCoopTrail &&
                data.TrailId >= FirstCoopTrailId && data.TrailId <= LastCoopTrailId &&
                data.MissionId <= LastCoopMissionId;
        }

        private static void Set(
            CustomCustomTrailLaunchOriginKind originValue,
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
            }
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
