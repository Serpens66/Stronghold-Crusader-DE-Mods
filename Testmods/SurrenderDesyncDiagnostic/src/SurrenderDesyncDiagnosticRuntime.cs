using APIShared;
using BepInEx;
using BepInEx.Logging;
using BugfixesAndQoL;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Buffers;

namespace SurrenderDesyncDiagnostic
{
    internal sealed unsafe class SurrenderDesyncDiagnosticRuntime
    {
        private readonly object gate = new object();
        private readonly ManualLogSource log;
        private readonly Queue<string> preroll = new Queue<string>();
        private readonly Queue<string> eventHistory = new Queue<string>();
        private readonly ResyncDiagnosticHistory chores = new ResyncDiagnosticHistory();
        private readonly List<SurrenderIdentity> surrenderLords = new List<SurrenderIdentity>();
        private readonly HashSet<string> captureGaps = new HashSet<string>(StringComparer.Ordinal);
        private DeferredTraceRecorder recorder;
        private string traceBase;
        private long sequence;
        private int lastMapTick = -1;
        private int lastDirectorTick = -1;
        private bool active;
        private bool resyncStarted;
        private bool incomplete;
        private bool postCleanupMarker;
        private bool probeDone;
        private long tickCount;
        private long tickElapsed;
        private long tickMax;

        private struct SurrenderIdentity
        {
            internal int Player, Unit, Global;
            internal SurrenderIdentity(int player, int unit, int global)
            { Player = player; Unit = unit; Global = global; }
        }

        internal SurrenderDesyncDiagnosticRuntime(ManualLogSource logger) { log = logger; }

        internal void Initialize()
        {
            recorder = new DeferredTraceRecorder(log);
            SurrenderDiagnosticBridge.SurrenderPhase += OnSurrenderPhase;
            SurrenderDiagnosticBridge.SpectatorPhase += OnSpectatorPhase;
            SurrenderDiagnosticBridge.SurrenderExecuted += OnSurrender;
            SurrenderDiagnosticBridge.SpectatorExecuted += OnSpectator;
            SurrenderDiagnosticBridge.ChoresSent += OnChores;
            SurrenderDiagnosticBridge.ResyncStateChanged += OnResync;
            GameTimeManagerAPI.Instance.OnTick += OnTick;
            if (ApiShared.Current.TryGetMissionLifecycle(SurrenderDesyncDiagnosticPlugin.PluginGuid,
                out IMissionLifecycleCapability mission, out NativeCapabilityDiagnostic missionDiagnostic))
            {
                if (!mission.TryRegisterObserver("surrender-diagnostic-mission",
                    OnMission, OnMission, OnMission, out missionDiagnostic))
                    Log("MISSION_OBSERVER_UNAVAILABLE: " + missionDiagnostic?.Reason);
            }
            else Log("MISSION_OBSERVER_UNAVAILABLE: " + missionDiagnostic?.Reason);
            if (ApiShared.Current.TryGetPlayerDefeat(SurrenderDesyncDiagnosticPlugin.PluginGuid,
                out IPlayerDefeatCapability capability, out NativeCapabilityDiagnostic diagnostic))
            {
                if (!capability.TryRegisterObserver("lord-death-diagnostic", OnLordDeath,
                    OnDefeat, out diagnostic)) Log("DEFEAT_OBSERVER_UNAVAILABLE: " + diagnostic?.Reason);
            }
            else Log("DEFEAT_OBSERVER_UNAVAILABLE: " + diagnostic?.Reason);
            Log("READY: persistent event subscriptions installed");
        }

        private void OnTick(int directorTick)
        {
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            lock (gate)
            {
                try
                {
                    int mapTick = MapTick();
                    if (SurrenderTracePolicy.MapRestarted(lastMapTick, mapTick)) ResetMap(mapTick);
                    lastMapTick = mapTick;
                    lastDirectorTick = directorTick;
                    if (!postCleanupMarker)
                    {
                        postCleanupMarker = true;
                        Log($"POST_STARTUP_RUNTIME_TICK map={mapTick} director={directorTick}");
                    }
                    if (!probeDone && !active && mapTick > 0) Probe(mapTick, directorTick);
                    if (active) Capture("pre-native-tick", mapTick, directorTick);
                    else
                    {
                        string pre = $"P\t{mapTick}\t{directorTick}\t{++sequence}\t" + PrerollState();
                        if (preroll.Count == 64) preroll.Dequeue();
                        preroll.Enqueue(pre);
                    }
                }
                catch (Exception ex) { Incomplete("TICK", ex); }
                finally
                {
                    long elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - started;
                    tickCount++;
                    tickElapsed += elapsed;
                    if (elapsed > tickMax) tickMax = elapsed;
                }
            }
        }

        private void OnSurrenderPhase(string phase, int player, int unit, int global, int tick)
        {
            lock (gate)
            {
                try
                {
                    if (phase == "confirmed" || phase == "host-request-accepted" || phase == "before-kill")
                        Arm(phase, player, unit, global, tick);
                    Event("surrender-" + phase, tick, $"player={player},unit={unit},global={global}");
                    if (active && (phase == "before-kill" || phase == "after-kill"))
                        Capture(phase, tick, lastDirectorTick);
                }
                catch (Exception ex) { Incomplete("SURRENDER", ex); }
            }
        }

        private void OnSpectatorPhase(string phase, int player, int local, int tick)
        {
            lock (gate)
            {
                try
                {
                    Event("spectator-" + phase, tick, $"player={player},local={local}");
                    if (active) Capture("spectator-" + phase, tick, lastDirectorTick);
                }
                catch (Exception ex) { Incomplete("SPECTATOR", ex); }
            }
        }

        private void OnSurrender(int player, int unit, int global, int tick)
        {
            lock (gate)
            {
                try
                {
                    Arm("surrender-chore", player, unit, global, tick);
                    Event("surrender-chore", tick, $"player={player},unit={unit},global={global}");
                }
                catch (Exception ex) { Incomplete("SURRENDER_CHORE", ex); }
            }
        }

        private void OnSpectator(long session, int player, int deathTick, int executionTick, int local)
        {
            lock (gate)
            {
                try { Event("spectator-chore", executionTick,
                    $"session={session},player={player},deathTick={deathTick},local={local}"); }
                catch (Exception ex) { Incomplete("SPECTATOR_CHORE", ex); }
            }
        }

        private void OnLordDeath(PlayerLordDeathNotification notice)
        {
            if (notice == null) return;
            lock (gate)
            {
                try
                {
                    bool surrender = false;
                    foreach (SurrenderIdentity lord in surrenderLords)
                        if (SurrenderTracePolicy.IsSurrenderLord(lord.Player, lord.Unit, lord.Global,
                            notice.PlayerId, notice.LordUnitId, notice.LordGlobalId))
                        { surrender = true; break; }
                    if (!surrender && !active)
                        Arm("ordinary-lord-death", -1, notice.LordUnitId, notice.LordGlobalId, MapTick());
                    Event(surrender ? "surrender-lord-death" : "ordinary-lord-death", MapTick(),
                        $"player={notice.PlayerId},unit={notice.LordUnitId},global={notice.LordGlobalId},observerDirectorTick={notice.SimulationTick}");
                }
                catch (Exception ex) { Incomplete("LORD_DEATH", ex); }
            }
        }

        private void OnDefeat(PlayerDefeatNotification notice)
        {
            if (notice == null) return;
            lock (gate)
            {
                try { Event("official-defeat", MapTick(),
                    $"player={notice.PlayerId},observerDirectorTick={notice.SimulationTick}"); }
                catch (Exception ex) { Incomplete("DEFEAT", ex); }
            }
        }

        private void OnMission(MissionLifecycleNotification notice)
        {
            if (notice == null) return;
            lock (gate)
            {
                try
                {
                    if (notice.Kind == MissionLifecycleKind.End && active)
                        Finish("map-end", MapTick());
                    else if (notice.Kind == MissionLifecycleKind.Start && (lastMapTick >= 0 || probeDone))
                        ResetMap(MapTick());
                }
                catch (Exception ex) { Incomplete("MISSION", ex); }
            }
        }

        private void OnChores(byte[] buffer)
        {
            lock (gate)
            {
                try
                {
                    int tick = MapTick();
                    chores.AddBuffer(buffer, tick, out bool start, out bool end, out string description);
                    if (active || start || end) Event("chores-sent", tick, description);
                    if (start) { resyncStarted = true; Event("resync-send-start", tick, description); }
                    if (end) Event("resync-send-end", tick, description);
                }
                catch (Exception ex) { Incomplete("CHORES", ex); }
            }
        }

        private void OnResync(bool before, bool now, int tick, int section, int layer)
        {
            lock (gate)
            {
                bool end = SurrenderTracePolicy.FirstResyncFinished(resyncStarted || now, before, now);
                try
                {
                    if (now) resyncStarted = true;
                    Event("resync-state", tick, $"before={before},now={now},section={section},layer={layer}");
                    // Copy both histories while holding the same lock used by every producer.
                    string[] choreCopy = chores.GetBuffers();
                    string[] eventCopy = eventHistory.ToArray();
                    if (active)
                    {
                        foreach (string line in choreCopy) QueueLine("H\tchore\t" + Escape(line));
                        foreach (string line in eventCopy) QueueLine("H\tevent\t" + Escape(line));
                        Capture("resync-state", tick, lastDirectorTick);
                    }
                }
                catch (Exception ex) { Incomplete("RESYNC", ex); }
                finally { if (end && active) Finish("first-resync-ended", tick); }
            }
        }

        private void Arm(string reason, int player, int unit, int global, int tick)
        {
            if (player > 0 && (unit > 0 || global > 0))
            {
                bool known = false;
                foreach (SurrenderIdentity lord in surrenderLords)
                    if (lord.Player == player && lord.Unit == unit && lord.Global == global)
                    { known = true; break; }
                if (!known) surrenderLords.Add(new SurrenderIdentity(player, unit, global));
            }
            if (active) return;
            if (!probeDone && tick > 0) Probe(tick, lastDirectorTick);
            active = true;
            incomplete = false;
            resyncStarted = false;
            captureGaps.Clear();
            traceBase = Path.Combine(Paths.PluginPath, "SurrenderDesyncDiagnostic_Serp", "Traces",
                "trace-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" +
                System.Diagnostics.Process.GetCurrentProcess().Id);
            recorder.Enqueue(new TraceWork { Kind = TraceWorkKind.Start, TraceBase = traceBase });
            foreach (string line in preroll.ToArray()) QueueLine(line);
            preroll.Clear();
            Event("capture-start", tick, $"reason={reason},player={player},unit={unit},global={global}");
            Capture("baseline", tick, lastDirectorTick);
            Log("CAPTURE_STARTED " + traceBase + " map=" + tick);
        }

        private void ResetMap(int tick)
        {
            if (active) Finish("map-end", lastMapTick);
            active = resyncStarted = incomplete = probeDone = false;
            surrenderLords.Clear(); captureGaps.Clear();
            preroll.Clear(); eventHistory.Clear(); chores.Reset();
            tickCount = tickElapsed = tickMax = 0;
            Log("MAP_RESET map=" + tick);
        }

        private void Finish(string reason, int tick)
        {
            try { Event("capture-end", tick, reason + ",incomplete=" + incomplete); }
            catch (Exception ex) { Incomplete("FINISH_EVENT", ex); }
            recorder.Enqueue(new TraceWork { Kind = TraceWorkKind.Finish, Reason = reason });
            active = false; resyncStarted = false;
            double average = tickCount == 0 ? 0 :
                1000.0 * tickElapsed / (System.Diagnostics.Stopwatch.Frequency * tickCount);
            double maximum = 1000.0 * tickMax / System.Diagnostics.Stopwatch.Frequency;
            Log("CAPTURE_QUEUED " + traceBase + " reason=" + reason + " incomplete=" + incomplete +
                " tickAverageMs=" + average.ToString("F2", CultureInfo.InvariantCulture) +
                " tickMaxMs=" + maximum.ToString("F2", CultureInfo.InvariantCulture));
        }

        private void Probe(int mapTick, int directorTick)
        {
            probeDone = true;
            string probeBase = Path.Combine(Paths.PluginPath, "SurrenderDesyncDiagnostic_Serp", "Traces",
                "probe-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" +
                System.Diagnostics.Process.GetCurrentProcess().Id);
            recorder.Enqueue(new TraceWork { Kind = TraceWorkKind.Start, TraceBase = probeBase, Probe = true });
            EnqueueSnapshot(CreateSnapshot("probe", mapTick, directorTick), probeBase);
            recorder.Enqueue(new TraceWork { Kind = TraceWorkKind.Finish, Reason = "probe" });
        }

        private void Capture(string phase, int mapTick, int directorTick)
        {
            if (!active) return;
            if (recorder.FailedFor(traceBase))
            {
                Incomplete("WRITER_FAILED", null);
                Finish("writer-failed", mapTick);
                return;
            }
            EnqueueSnapshot(CreateSnapshot(phase, mapTick, directorTick), traceBase);
        }

        private void EnqueueSnapshot(TraceSnapshot snapshot, string destination)
        {
            if (recorder.Enqueue(new TraceWork { Kind = TraceWorkKind.Capture,
                TraceBase = destination, Snapshot = snapshot })) return;
            snapshot.Release();
            Incomplete("WRITER_FAILED", null);
            if (active) Finish("writer-failed", snapshot.MapTick);
            else recorder.Enqueue(new TraceWork { Kind = TraceWorkKind.Line,
                Text = "I\t" + snapshot.MapTick + "\t" + snapshot.DirectorTick + "\tWRITER_FAILED" });
        }

        private TraceSnapshot CreateSnapshot(string phase, int mapTick, int directorTick)
        {
            var snapshot = new TraceSnapshot { Phase = phase, MapTick = mapTick,
                DirectorTick = directorTick, Order = ++sequence };
            Stage(snapshot, "players", StagePlayers);
            Stage(snapshot, "units", StageUnits);
            Stage(snapshot, "buildings", StageBuildings);
            Stage(snapshot, "tribes", StageTribes);
            Stage(snapshot, "projectiles", StageProjectiles);
            Stage(snapshot, "vegetation", StageVegetation);
            Stage(snapshot, "path-components", category => StageGrid(category, PathApi().GetPathComponentGrid()));
            Stage(snapshot, "path-edges", category => StageGrid(category, PathApi().GetPathEdgeMaskGrid()));
            Stage(snapshot, "moat-work", category => StageGrid(category, PathApi().GetMoatWorkTaskIndexGrid()));
            Stage(snapshot, "connections", StageConnections);
            try
            {
                snapshot.LocalState = $"localPlayer={GamePlayerManagerAPI.Instance?.GetLocalPlayerId() ?? -1},spectator={GameData.Instance?.lastGameState?.spectatorMode ?? -1}";
            }
            catch (Exception ex)
            {
                snapshot.Categories.Add(new TraceCategory { Name = "local-ui", Error = ex.ToString() });
            }
            return snapshot;
        }

        private static void Stage(TraceSnapshot snapshot, string name, Action<TraceCategory> capture)
        {
            var category = new TraceCategory { Name = name };
            try { capture(category); }
            catch (Exception ex)
            {
                category.Release();
                category.Error = ex.ToString();
            }
            snapshot.Categories.Add(category);
        }

        private static void AddRecord(TraceCategory category, string key, Type type, IntPtr pointer, int size)
        {
            byte[] data = ArrayPool<byte>.Shared.Rent(size);
            try { Marshal.Copy(pointer, data, 0, size); }
            catch { ArrayPool<byte>.Shared.Return(data); throw; }
            category.Records.Add(new TraceRecord { Key = key, Type = type, Data = data,
                DataLength = size });
        }

        private static void StagePlayers(TraceCategory category)
        {
            var api = GamePlayerManagerAPI.Instance;
            if (api == null) throw new InvalidOperationException("Player API unavailable");
            int found = 0;
            for (int id = 1; id <= 8; id++)
            {
                string key = "players/" + id + "/0";
                if (api.TryGetPlayerResourcesById(id, out GamePlayerResources* value) && value != null)
                {
                    AddRecord(category, key, typeof(GamePlayerResources), new IntPtr(value), sizeof(GamePlayerResources));
                    found++;
                }
                else category.Records.Add(new TraceRecord { Key = key, Text = "missing=1" });
            }
            if (found == 0) throw new InvalidOperationException("No player record available");
        }

        private static void StageUnits(TraceCategory category)
        {
            Span<GameUnit> span = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            fixed (GameUnit* pointer = span)
                for (int index = 0; index < span.Length; index++)
                    if (pointer[index].r_GlobalId != 0 || (int)pointer[index].r_AliveState != 0)
                        AddRecord(category, $"units/{index + 1}/{pointer[index].r_GlobalId}",
                            typeof(GameUnit), new IntPtr(pointer + index), sizeof(GameUnit));
        }

        private static void StageBuildings(TraceCategory category)
        {
            Span<GameBuilding> span = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            fixed (GameBuilding* pointer = span)
                for (int index = 0; index < span.Length; index++)
                    if (pointer[index].r_GlobalId != 0 || (int)pointer[index].r_AliveState != 0)
                        AddRecord(category, $"buildings/{index + 1}/{pointer[index].r_GlobalId}",
                            typeof(GameBuilding), new IntPtr(pointer + index), sizeof(GameBuilding));
        }

        private static void StageTribes(TraceCategory category)
        {
            Span<GameTribe> span = GameTribeManagerAPI.Instance.GetTribeAsSpan();
            fixed (GameTribe* pointer = span)
                for (int index = 0; index < span.Length; index++)
                    if (pointer[index].r_GlobalId != 0 || (int)pointer[index].r_AliveState != 0)
                        AddRecord(category, $"tribes/{index + 1}/{pointer[index].r_GlobalId}",
                            typeof(GameTribe), new IntPtr(pointer + index), sizeof(GameTribe));
        }

        private static void StageProjectiles(TraceCategory category)
        {
            Span<GameProjectile> span = GameProjectileManagerAPI.Instance.GetProjectilesAsSpan();
            fixed (GameProjectile* pointer = span)
                for (int index = 0; index < span.Length; index++)
                    if (pointer[index].r_GlobalId != 0 || (int)pointer[index].r_AliveState != 0)
                        AddRecord(category, $"projectiles/{index + 1}/{pointer[index].r_GlobalId}",
                            typeof(GameProjectile), new IntPtr(pointer + index), sizeof(GameProjectile));
        }

        private static void StageVegetation(TraceCategory category)
        {
            Span<GameVegetation> span = GameVegetationManagerAPI.Instance.GetVegetationAsSpan();
            fixed (GameVegetation* pointer = span)
                for (int index = 0; index < span.Length; index++)
                    if (pointer[index].r_GlobalId != 0 || (int)pointer[index].r_AliveState != 0)
                        AddRecord(category, $"vegetation/{index + 1}/{pointer[index].r_GlobalId}",
                            typeof(GameVegetation), new IntPtr(pointer + index), sizeof(GameVegetation));
        }

        private static GamePathingManagerAPI PathApi()
        {
            var api = GamePathingManagerAPI.Instance;
            if (api == null) throw new InvalidOperationException("Pathing API unavailable");
            return api;
        }

        private static void StageConnections(TraceCategory category)
        {
            Span<PathConnectionRecord> span = PathApi().GetPathConnectionRecords();
            fixed (PathConnectionRecord* pointer = span)
                for (int index = 0; index < span.Length; index++)
                    if (pointer[index].r_IsActive != 0 || pointer[index].r_RecordGlobalId != 0)
                        AddRecord(category, $"connections/{index}/{pointer[index].r_RecordGlobalId}",
                            typeof(PathConnectionRecord), new IntPtr(pointer + index), sizeof(PathConnectionRecord));
        }

        private static void StageGrid(TraceCategory category, Span<ushort> span)
        {
            if (span.Length == 0) throw new InvalidOperationException(category.Name + " grid unavailable");
            category.GridLength = span.Length;
            category.GridElementBytes = 2;
            int length = checked(span.Length * 2);
            byte[] data = ArrayPool<byte>.Shared.Rent(length);
            try
            {
                fixed (ushort* pointer = span) Marshal.Copy(new IntPtr(pointer), data, 0, length);
                category.Grid = data;
            }
            catch { ArrayPool<byte>.Shared.Return(data); throw; }
        }

        private static void StageGrid(TraceCategory category, Span<byte> span)
        {
            if (span.Length == 0) throw new InvalidOperationException(category.Name + " grid unavailable");
            category.GridLength = span.Length;
            category.GridElementBytes = 1;
            byte[] data = ArrayPool<byte>.Shared.Rent(span.Length);
            try
            {
                fixed (byte* pointer = span) Marshal.Copy(new IntPtr(pointer), data, 0, span.Length);
                category.Grid = data;
            }
            catch { ArrayPool<byte>.Shared.Return(data); throw; }
        }

        private struct FieldSpec
        {
            internal string Name;
            internal int Offset;
            internal TypeCode Kind;
        }

        private static readonly Dictionary<Type, FieldSpec[]> fieldCache = new Dictionary<Type, FieldSpec[]>();
        internal static string Fields(Type type, IntPtr value)
        {
            FieldSpec[] selected;
            lock (fieldCache)
            {
                if (!fieldCache.TryGetValue(type, out selected))
                {
                    var found = new List<FieldSpec>();
                    int size = Marshal.SizeOf(type);
                    foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (!field.Name.StartsWith("r_", StringComparison.Ordinal) ||
                            field.Name.IndexOf("Unknown", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            field.Name.IndexOf("Hover", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            field.Name.IndexOf("Sprite", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            field.Name.IndexOf("Animation", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            field.Name.IndexOf("HealthBar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            field.Name.StartsWith("r_p_", StringComparison.Ordinal) ||
                            field.Name.IndexOf("Pointer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            field.Name.IndexOf("Address", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            field.FieldType.IsPointer ||
                            !(field.FieldType.IsPrimitive || field.FieldType.IsEnum)) continue;
                        Type scalarType = field.FieldType.IsEnum ? Enum.GetUnderlyingType(field.FieldType) : field.FieldType;
                        TypeCode kind = Type.GetTypeCode(scalarType);
                        int width = Width(kind);
                        if (width == 0) continue;
                        foreach (object attribute in field.GetCustomAttributes(false))
                            if (attribute.GetType().Name == "LuaExposedAttribute")
                            {
                                int offset = Marshal.OffsetOf(type, field.Name).ToInt32();
                                if (offset < 0 || offset > size - width)
                                    throw new InvalidOperationException(type.Name + "." + field.Name + " offset outside record");
                                found.Add(new FieldSpec { Name = field.Name, Offset = offset, Kind = kind });
                                break;
                            }
                    }
                    found.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
                    fieldCache[type] = selected = found.ToArray();
                }
            }
            var result = new StringBuilder(selected.Length * 16);
            foreach (FieldSpec field in selected)
                result.Append(field.Name).Append('=').Append(ReadScalar(value, field)).Append(',');
            return result.ToString();
        }

        private static int Width(TypeCode kind)
        {
            switch (kind)
            {
                case TypeCode.Boolean: case TypeCode.Byte: case TypeCode.SByte: return 1;
                case TypeCode.Char: case TypeCode.Int16: case TypeCode.UInt16: return 2;
                case TypeCode.Int32: case TypeCode.UInt32: case TypeCode.Single: return 4;
                case TypeCode.Int64: case TypeCode.UInt64: case TypeCode.Double: return 8;
                default: return 0;
            }
        }

        private static string ReadScalar(IntPtr pointer, FieldSpec field)
        {
            int offset = field.Offset;
            switch (field.Kind)
            {
                case TypeCode.Boolean: return (Marshal.ReadByte(pointer, offset) != 0).ToString();
                case TypeCode.Byte: return Marshal.ReadByte(pointer, offset).ToString(CultureInfo.InvariantCulture);
                case TypeCode.SByte: return unchecked((sbyte)Marshal.ReadByte(pointer, offset)).ToString(CultureInfo.InvariantCulture);
                case TypeCode.Char: return unchecked((ushort)Marshal.ReadInt16(pointer, offset)).ToString(CultureInfo.InvariantCulture);
                case TypeCode.Int16: return Marshal.ReadInt16(pointer, offset).ToString(CultureInfo.InvariantCulture);
                case TypeCode.UInt16: return unchecked((ushort)Marshal.ReadInt16(pointer, offset)).ToString(CultureInfo.InvariantCulture);
                case TypeCode.Int32: case TypeCode.Single:
                    return Marshal.ReadInt32(pointer, offset).ToString(CultureInfo.InvariantCulture);
                case TypeCode.UInt32: return unchecked((uint)Marshal.ReadInt32(pointer, offset)).ToString(CultureInfo.InvariantCulture);
                case TypeCode.Int64: case TypeCode.Double:
                    return Marshal.ReadInt64(pointer, offset).ToString(CultureInfo.InvariantCulture);
                case TypeCode.UInt64: return unchecked((ulong)Marshal.ReadInt64(pointer, offset)).ToString(CultureInfo.InvariantCulture);
                default: throw new InvalidOperationException("Unsupported scalar: " + field.Kind);
            }
        }

        private string PrerollState()
        {
            var value = new StringBuilder();
            var api = GamePlayerManagerAPI.Instance;
            for (int id = 1; id <= 8; id++)
                if (api != null && api.TryGetPlayerResourcesById(id, out GamePlayerResources* player) && player != null)
                    value.Append(id).Append(':').Append(player->r_LordUnitId).Append(':')
                        .Append((int)player->r_WinLossState).Append(';');
            return value.ToString();
        }

        private void Event(string kind, int tick, string detail)
        {
            string line = $"E\t{tick}\t{lastDirectorTick}\t{++sequence}\t{kind}\t{Escape(detail)}";
            if (eventHistory.Count == 128) eventHistory.Dequeue();
            eventHistory.Enqueue(line);
            if (active) QueueLine(line);
            if (kind != "chores-sent")
                Log(kind + " map=" + tick + " director=" + lastDirectorTick + " " + detail);
        }

        private void QueueLine(string line)
        {
            recorder.Enqueue(new TraceWork { Kind = TraceWorkKind.Line, Text = line });
        }

        private void Incomplete(string kind, Exception ex)
        {
            if (captureGaps.Add(kind))
            {
                Log("TRACE_INCOMPLETE " + kind + " " + ex);
                if (active) QueueLine("I\t" + MapTick() + "\t" + lastDirectorTick +
                    "\t" + (++sequence) + "\t" + kind);
            }
            incomplete = true;
        }

        private static int MapTick() => GameTimeManagerAPI.Instance?.GetElapsedMapTicks() ?? -1;
        private static string Escape(string value) => (value ?? "").Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
        private void Log(string message) => Shared.DebugLogHelper.LogInfo(log, "[SurrenderDiag] " + message);
    }
}
