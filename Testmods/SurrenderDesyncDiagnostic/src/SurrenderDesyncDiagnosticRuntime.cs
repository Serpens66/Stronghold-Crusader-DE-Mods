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

namespace SurrenderDesyncDiagnostic
{
    internal sealed unsafe class SurrenderDesyncDiagnosticRuntime
    {
        private readonly object gate = new object();
        private readonly ManualLogSource log;
        private readonly Queue<string> preroll = new Queue<string>();
        private readonly Queue<string> eventHistory = new Queue<string>();
        private readonly Dictionary<string, string> previous = new Dictionary<string, string>();
        private readonly ResyncDiagnosticHistory chores = new ResyncDiagnosticHistory();
        private readonly List<SurrenderIdentity> surrenderLords = new List<SurrenderIdentity>();
        private readonly HashSet<string> captureGaps = new HashSet<string>(StringComparer.Ordinal);
        private StreamWriter writer;
        private string traceBase;
        private long sequence;
        private int segment;
        private int lastMapTick = -1;
        private int lastDirectorTick = -1;
        private bool active;
        private bool resyncStarted;
        private bool incomplete;
        private bool postCleanupMarker;
        private bool probeDone;

        private struct SurrenderIdentity
        {
            internal int Player, Unit, Global;
            internal SurrenderIdentity(int player, int unit, int global)
            { Player = player; Unit = unit; Global = global; }
        }

        internal SurrenderDesyncDiagnosticRuntime(ManualLogSource logger) { log = logger; }

        internal void Initialize()
        {
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
                Event("spectator-" + phase, tick, $"player={player},local={local}");
                try { if (active) Capture("spectator-" + phase, tick, lastDirectorTick); }
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
        { lock (gate) Event("spectator-chore", executionTick,
            $"session={session},player={player},deathTick={deathTick},local={local}"); }

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
                    string description = ResyncDiagnosticHistory.DescribeBuffer(buffer, tick,
                        out bool start, out bool end);
                    chores.AddBuffer(buffer, tick, out _, out _);
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
                        foreach (string line in choreCopy) Write("H\tchore\t" + Escape(line));
                        foreach (string line in eventCopy) Write("H\tevent\t" + Escape(line));
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
            segment = 0;
            previous.Clear();
            captureGaps.Clear();
            traceBase = Path.Combine(Paths.PluginPath, "SurrenderDesyncDiagnostic_Serp", "Traces",
                "trace-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" +
                System.Diagnostics.Process.GetCurrentProcess().Id);
            OpenSegment();
            Write("V\t2\tmapTick\tdirectorTick\tsequence\tphase\tcategory/object\tfields");
            Write("G\tunknown-fields,pointers,padding,nested-or-array-fields,visual-presentation-fields,unavailable-APIs-excluded;local-UI-separate");
            foreach (string line in preroll.ToArray()) Write(line);
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
            previous.Clear(); preroll.Clear(); eventHistory.Clear(); chores.Reset();
            Log("MAP_RESET map=" + tick);
        }

        private void Finish(string reason, int tick)
        {
            try { Event("capture-end", tick, reason + ",incomplete=" + incomplete); }
            catch (Exception ex) { Incomplete("FINISH_EVENT", ex); }
            try { writer?.Flush(); writer?.Dispose(); }
            catch (Exception ex) { Incomplete("CLOSE", ex); }
            finally { writer = null; active = false; resyncStarted = false; }
            Log("CAPTURE_FINISHED " + traceBase + " reason=" + reason + " incomplete=" + incomplete);
        }

        private static readonly string[] categories = { "players", "units", "buildings", "tribes",
            "projectiles", "vegetation", "path-components", "path-edges", "moat-work", "connections" };

        private void Probe(int mapTick, int directorTick)
        {
            probeDone = true;
            active = true;
            incomplete = false;
            segment = 0;
            traceBase = Path.Combine(Paths.PluginPath, "SurrenderDesyncDiagnostic_Serp", "Traces",
                "probe-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" +
                System.Diagnostics.Process.GetCurrentProcess().Id);
            previous.Clear(); captureGaps.Clear();
            int hashCount = 0;
            try
            {
                OpenSegment();
                Write("V\t2\tprobe\tmapTick\tdirectorTick\tsequence\tphase\tcategory/object\tfields");
                hashCount = Capture("probe", mapTick, directorTick);
            }
            catch (Exception ex) { Incomplete("PROBE", ex); }
            finally
            {
                try { writer?.Flush(); writer?.Dispose(); }
                catch (Exception ex) { Incomplete("PROBE_CLOSE", ex); }
                writer = null;
                string file = traceBase + "-0001.tsv";
                bool hasChanges = false, hasHashes = false;
                try
                {
                    foreach (string line in File.ReadLines(file))
                    {
                        if (line.StartsWith("C\t", StringComparison.Ordinal)) hasChanges = true;
                        if (line.StartsWith("S\t", StringComparison.Ordinal)) hasHashes = true;
                        if (hasChanges && hasHashes) break;
                    }
                }
                catch (Exception ex) { Incomplete("PROBE_VERIFY", ex); }
                if (!incomplete && hasChanges && hasHashes && hashCount == categories.Length)
                    Log("STATE_PROBE_OK map=" + mapTick + " categories=" + hashCount + " file=" + file);
                else
                    Log("STATE_PROBE_FAILED map=" + mapTick + " categories=" + hashCount +
                        " changes=" + hasChanges + " hashes=" + hasHashes + " incomplete=" + incomplete + " file=" + file);
                active = false;
                incomplete = false;
                previous.Clear(); captureGaps.Clear();
            }
        }

        private int Capture(string phase, int mapTick, int directorTick)
        {
            if (!active) return 0;
            var rows = new SortedDictionary<string, string>(StringComparer.Ordinal);
            var failed = new HashSet<string>(StringComparer.Ordinal);
            CaptureCategory(rows, failed, "players", CapturePlayers);
            CaptureCategory(rows, failed, "units", CaptureUnits);
            CaptureCategory(rows, failed, "buildings", CaptureBuildings);
            CaptureCategory(rows, failed, "tribes", CaptureTribes);
            CaptureCategory(rows, failed, "projectiles", CaptureProjectiles);
            CaptureCategory(rows, failed, "vegetation", CaptureVegetation);
            CaptureCategory(rows, failed, "path-components", (result) => Grid(result, "path-components", PathApi().GetPathComponentGrid()));
            CaptureCategory(rows, failed, "path-edges", (result) => Grid(result, "path-edges", PathApi().GetPathEdgeMaskGrid()));
            CaptureCategory(rows, failed, "moat-work", (result) => Grid(result, "moat-work", PathApi().GetMoatWorkTaskIndexGrid()));
            CaptureCategory(rows, failed, "connections", CaptureConnections);
            var hashes = new SortedDictionary<string, StringBuilder>(StringComparer.Ordinal);
            foreach (string category in categories)
                if (!failed.Contains(category)) hashes[category] = new StringBuilder();
            foreach (var row in rows)
            {
                string category = row.Key.Substring(0, row.Key.IndexOf('/'));
                StringBuilder builder = hashes[category];
                builder.Append(row.Key).Append('=').Append(row.Value).Append(';');
                if (!previous.TryGetValue(row.Key, out string old) || old != row.Value)
                    Write($"C\t{mapTick}\t{directorTick}\t{++sequence}\t{phase}\t{row.Key}\t{Escape(row.Value)}");
            }
            foreach (var old in previous)
                if (!rows.ContainsKey(old.Key))
                    Write($"C\t{mapTick}\t{directorTick}\t{++sequence}\t{phase}\t{old.Key}\t<removed>");
            foreach (var hash in hashes)
                Write($"S\t{mapTick}\t{directorTick}\t{++sequence}\t{phase}\t{hash.Key}\t{ResyncDiagnosticHistory.ComputeSha256(hash.Value.ToString())}");
            try
            {
                Write($"L\t{mapTick}\t{directorTick}\t{++sequence}\t{phase}\tlocalPlayer={GamePlayerManagerAPI.Instance?.GetLocalPlayerId() ?? -1},spectator={GameData.Instance?.lastGameState?.spectatorMode ?? -1}");
            }
            catch (Exception ex) { Incomplete("LOCAL_UI", ex); }
            previous.Clear();
            foreach (var row in rows) previous.Add(row.Key, row.Value);
            writer?.Flush();
            return hashes.Count;
        }

        private void CaptureCategory(IDictionary<string, string> rows, ISet<string> failed,
            string category, Action<IDictionary<string, string>> capture)
        {
            var categoryRows = new SortedDictionary<string, string>(StringComparer.Ordinal);
            try
            {
                capture(categoryRows);
                foreach (var row in categoryRows) rows.Add(row.Key, row.Value);
            }
            catch (Exception ex)
            {
                failed.Add(category);
                Incomplete("CAPTURE_" + category, ex);
            }
        }

        private static void CapturePlayers(IDictionary<string, string> rows)
        {
            var api = GamePlayerManagerAPI.Instance;
            if (api == null) throw new InvalidOperationException("Player API unavailable");
            int found = 0;
            for (int id = 1; id <= 8; id++)
                if (api.TryGetPlayerResourcesById(id, out GamePlayerResources* value) && value != null)
                {
                    rows[$"players/{id}/0"] = Fields(typeof(GamePlayerResources), new IntPtr(value));
                    found++;
                }
                else rows[$"players/{id}/0"] = "missing=1";
            if (found == 0) throw new InvalidOperationException("No player record available");
        }

        private static void CaptureUnits(IDictionary<string, string> rows)
        {
            Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            fixed (GameUnit* ptr = units)
                for (int i = 0; i < units.Length; i++)
                    if (ptr[i].r_GlobalId != 0 || (int)ptr[i].r_AliveState != 0)
                        rows[$"units/{i + 1}/{ptr[i].r_GlobalId}"] = Fields(typeof(GameUnit), new IntPtr(ptr + i));
        }

        private static void CaptureBuildings(IDictionary<string, string> rows)
        {
            Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            fixed (GameBuilding* ptr = buildings)
                for (int i = 0; i < buildings.Length; i++)
                    if (ptr[i].r_GlobalId != 0 || (int)ptr[i].r_AliveState != 0)
                        rows[$"buildings/{i + 1}/{ptr[i].r_GlobalId}"] = Fields(typeof(GameBuilding), new IntPtr(ptr + i));
        }

        private static void CaptureTribes(IDictionary<string, string> rows)
        {
            Span<GameTribe> tribes = GameTribeManagerAPI.Instance.GetTribeAsSpan();
            fixed (GameTribe* ptr = tribes)
                for (int i = 0; i < tribes.Length; i++)
                    if (ptr[i].r_GlobalId != 0 || (int)ptr[i].r_AliveState != 0)
                        rows[$"tribes/{i + 1}/{ptr[i].r_GlobalId}"] = Fields(typeof(GameTribe), new IntPtr(ptr + i));
        }

        private static void CaptureProjectiles(IDictionary<string, string> rows)
        {
            Span<GameProjectile> projectiles = GameProjectileManagerAPI.Instance.GetProjectilesAsSpan();
            fixed (GameProjectile* ptr = projectiles)
                for (int i = 0; i < projectiles.Length; i++)
                    if (ptr[i].r_GlobalId != 0 || (int)ptr[i].r_AliveState != 0)
                        rows[$"projectiles/{i + 1}/{ptr[i].r_GlobalId}"] = Fields(typeof(GameProjectile), new IntPtr(ptr + i));
        }

        private static void CaptureVegetation(IDictionary<string, string> rows)
        {
            Span<GameVegetation> plants = GameVegetationManagerAPI.Instance.GetVegetationAsSpan();
            fixed (GameVegetation* ptr = plants)
                for (int i = 0; i < plants.Length; i++)
                    if (ptr[i].r_GlobalId != 0 || (int)ptr[i].r_AliveState != 0)
                        rows[$"vegetation/{i + 1}/{ptr[i].r_GlobalId}"] = Fields(typeof(GameVegetation), new IntPtr(ptr + i));
        }

        private static GamePathingManagerAPI PathApi()
        {
            var api = GamePathingManagerAPI.Instance;
            if (api == null) throw new InvalidOperationException("Pathing API unavailable");
            return api;
        }

        private static void CaptureConnections(IDictionary<string, string> rows)
        {
            Span<PathConnectionRecord> connections = PathApi().GetPathConnectionRecords();
            fixed (PathConnectionRecord* ptr = connections)
                for (int i = 0; i < connections.Length; i++)
                    if (ptr[i].r_IsActive != 0 || ptr[i].r_RecordGlobalId != 0)
                        rows[$"connections/{i}/{ptr[i].r_RecordGlobalId}"] = Fields(typeof(PathConnectionRecord), new IntPtr(ptr + i));
        }

        private static void Grid<T>(IDictionary<string, string> rows, string category, Span<T> span) where T : struct
        {
            if (span.Length == 0) throw new InvalidOperationException(category + " grid unavailable");
            var value = new StringBuilder(2048);
            for (int start = 0; start < span.Length; start += 256)
            {
                value.Clear();
                for (int i = start; i < Math.Min(start + 256, span.Length); i++)
                    value.Append(span[i]).Append(',');
                rows[$"{category}/{start}/0"] = value.ToString();
            }
        }

        private struct FieldSpec
        {
            internal string Name;
            internal int Offset;
            internal TypeCode Kind;
        }

        private static readonly Dictionary<Type, FieldSpec[]> fieldCache = new Dictionary<Type, FieldSpec[]>();
        private static string Fields(Type type, IntPtr value)
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
            if (active) Write(line);
            if (kind != "chores-sent")
                Log(kind + " map=" + tick + " director=" + lastDirectorTick + " " + detail);
        }

        private void OpenSegment()
        {
            try
            {
                string path = traceBase + "-" + (++segment).ToString("D4") + ".tsv";
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                writer = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write,
                    FileShare.Read, 65536), new UTF8Encoding(false), 65536);
                writer.WriteLine("V\t2\tsegment=" + segment);
            }
            catch (Exception ex) { Incomplete("OPEN", ex); }
        }

        private void Write(string line)
        {
            if (writer == null) { if (active) Incomplete("NO_WRITER", null); return; }
            try
            {
                writer.WriteLine(line);
                if (writer.BaseStream.Position >= 32L * 1024 * 1024)
                { writer.Flush(); writer.Dispose(); writer = null; OpenSegment(); }
            }
            catch (Exception ex) { Incomplete("WRITE", ex); }
        }

        private void Incomplete(string kind, Exception ex)
        {
            if (captureGaps.Add(kind))
            {
                Log("TRACE_INCOMPLETE " + kind + " " + ex);
                try { writer?.WriteLine("I\t" + MapTick() + "\t" + lastDirectorTick +
                    "\t" + (++sequence) + "\t" + kind); writer?.Flush(); }
                catch { /* BepInEx log remains the durable error channel. */ }
            }
            incomplete = true;
        }

        private static int MapTick() => GameTimeManagerAPI.Instance?.GetElapsedMapTicks() ?? -1;
        private static string Escape(string value) => (value ?? "").Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
        private void Log(string message) => Shared.DebugLogHelper.LogInfo(log, "[SurrenderDiag] " + message);
    }
}
