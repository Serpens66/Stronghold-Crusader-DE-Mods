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
using System.Reflection;
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
        private StreamWriter writer;
        private string traceBase;
        private long sequence;
        private int segment;
        private int lastMapTick = -1;
        private int lastDirectorTick = -1;
        private int surrenderPlayer = -1;
        private int surrenderUnit = -1;
        private int surrenderGlobal = -1;
        private bool active;
        private bool finished;
        private bool resyncStarted;
        private bool incomplete;
        private bool postCleanupMarker;

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
                    if (active) Capture("pre-native-tick", mapTick, directorTick);
                    else if (!finished)
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
        { lock (gate) Event("surrender-chore", tick, $"player={player},unit={unit},global={global}"); }

        private void OnSpectator(long session, int player, int deathTick, int executionTick, int local)
        { lock (gate) Event("spectator-chore", executionTick,
            $"session={session},player={player},deathTick={deathTick},local={local}"); }

        private void OnLordDeath(PlayerLordDeathNotification notice)
        {
            if (notice == null) return;
            lock (gate)
            {
                bool surrender = SurrenderTracePolicy.IsSurrenderLord(
                    surrenderPlayer, surrenderUnit, surrenderGlobal,
                    notice.PlayerId, notice.LordUnitId, notice.LordGlobalId);
                if (!surrender && !active && !finished)
                    Arm("ordinary-lord-death", -1, notice.LordUnitId, notice.LordGlobalId, MapTick());
                Event(surrender ? "surrender-lord-death" : "ordinary-lord-death", MapTick(),
                    $"player={notice.PlayerId},unit={notice.LordUnitId},global={notice.LordGlobalId},observerDirectorTick={notice.SimulationTick}");
            }
        }

        private void OnDefeat(PlayerDefeatNotification notice)
        { if (notice != null) lock (gate) Event("official-defeat", MapTick(),
            $"player={notice.PlayerId},observerDirectorTick={notice.SimulationTick}"); }

        private void OnMission(MissionLifecycleNotification notice)
        {
            if (notice == null) return;
            lock (gate)
            {
                if (notice.Kind == MissionLifecycleKind.End && active)
                    Finish("map-end", MapTick());
                else if (notice.Kind == MissionLifecycleKind.Start && finished)
                    ResetMap(MapTick());
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
                    if (SurrenderTracePolicy.FirstResyncFinished(resyncStarted, before, now))
                        Finish("first-resync-ended", tick);
                }
                catch (Exception ex) { Incomplete("RESYNC", ex); }
            }
        }

        private void Arm(string reason, int player, int unit, int global, int tick)
        {
            if (active || finished) return;
            surrenderPlayer = player;
            surrenderUnit = unit;
            surrenderGlobal = global;
            active = true;
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
            active = finished = resyncStarted = incomplete = false;
            surrenderPlayer = surrenderUnit = surrenderGlobal = -1;
            previous.Clear(); preroll.Clear(); eventHistory.Clear(); chores.Reset();
            Log("MAP_RESET map=" + tick);
        }

        private void Finish(string reason, int tick)
        {
            Event("capture-end", tick, reason + ",incomplete=" + incomplete);
            active = false;
            finished = true;
            try { writer?.Flush(); writer?.Dispose(); writer = null; }
            catch (Exception ex) { Incomplete("CLOSE", ex); }
            Log("CAPTURE_FINISHED " + traceBase + " reason=" + reason + " incomplete=" + incomplete);
        }

        private void Capture(string phase, int mapTick, int directorTick)
        {
            if (!active) return;
            var rows = new SortedDictionary<string, string>(StringComparer.Ordinal);
            CapturePlayers(rows);
            CaptureObjects(rows);
            CapturePath(rows);
            var hashes = new SortedDictionary<string, StringBuilder>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                string category = row.Key.Substring(0, row.Key.IndexOf('/'));
                if (!hashes.TryGetValue(category, out StringBuilder builder))
                    hashes[category] = builder = new StringBuilder();
                builder.Append(row.Key).Append('=').Append(row.Value).Append(';');
                if (!previous.TryGetValue(row.Key, out string old) || old != row.Value)
                    Write($"C\t{mapTick}\t{directorTick}\t{++sequence}\t{phase}\t{row.Key}\t{Escape(row.Value)}");
            }
            foreach (var old in previous)
                if (!rows.ContainsKey(old.Key))
                    Write($"C\t{mapTick}\t{directorTick}\t{++sequence}\t{phase}\t{old.Key}\t<removed>");
            foreach (var hash in hashes)
                Write($"S\t{mapTick}\t{directorTick}\t{++sequence}\t{phase}\t{hash.Key}\t{ResyncDiagnosticHistory.ComputeSha256(hash.Value.ToString())}");
            Write($"L\t{mapTick}\t{directorTick}\t{++sequence}\t{phase}\tlocalPlayer={GamePlayerManagerAPI.Instance?.GetLocalPlayerId() ?? -1},spectator={GameData.Instance?.lastGameState?.spectatorMode ?? -1}");
            previous.Clear();
            foreach (var row in rows) previous.Add(row.Key, row.Value);
            writer?.Flush();
        }

        private static void CapturePlayers(IDictionary<string, string> rows)
        {
            var api = GamePlayerManagerAPI.Instance;
            for (int id = 1; id <= 8; id++)
                if (api != null && api.TryGetPlayerResourcesById(id, out GamePlayerResources* value) && value != null)
                    rows[$"players/{id}/0"] = Fields(*value);
                else rows[$"players/{id}/0"] = "missing=1";
        }

        private static void CaptureObjects(IDictionary<string, string> rows)
        {
            Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            for (int i = 0; i < units.Length; i++)
                if (units[i].r_GlobalId != 0 || (int)units[i].r_AliveState != 0)
                    rows[$"units/{i + 1}/{units[i].r_GlobalId}"] = Fields(units[i]);
            Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            for (int i = 0; i < buildings.Length; i++)
                if (buildings[i].r_GlobalId != 0 || (int)buildings[i].r_AliveState != 0)
                    rows[$"buildings/{i + 1}/{buildings[i].r_GlobalId}"] = Fields(buildings[i]);
            Span<GameTribe> tribes = GameTribeManagerAPI.Instance.GetTribeAsSpan();
            for (int i = 0; i < tribes.Length; i++)
                if (tribes[i].r_GlobalId != 0 || (int)tribes[i].r_AliveState != 0)
                    rows[$"tribes/{i + 1}/{tribes[i].r_GlobalId}"] = Fields(tribes[i]);
            Span<GameProjectile> projectiles = GameProjectileManagerAPI.Instance.GetProjectilesAsSpan();
            for (int i = 0; i < projectiles.Length; i++)
                if (projectiles[i].r_GlobalId != 0 || (int)projectiles[i].r_AliveState != 0)
                    rows[$"projectiles/{i + 1}/{projectiles[i].r_GlobalId}"] = Fields(projectiles[i]);
            Span<GameVegetation> plants = GameVegetationManagerAPI.Instance.GetVegetationAsSpan();
            for (int i = 0; i < plants.Length; i++)
                if (plants[i].r_GlobalId != 0 || (int)plants[i].r_AliveState != 0)
                    rows[$"vegetation/{i + 1}/{plants[i].r_GlobalId}"] = Fields(plants[i]);
        }

        private static void CapturePath(IDictionary<string, string> rows)
        {
            var api = GamePathingManagerAPI.Instance;
            if (api == null) { rows["path/0/0"] = "unavailable=1"; return; }
            Grid(rows, "path-components", api.GetPathComponentGrid());
            Grid(rows, "path-edges", api.GetPathEdgeMaskGrid());
            Grid(rows, "moat-work", api.GetMoatWorkTaskIndexGrid());
            Span<PathConnectionRecord> connections = api.GetPathConnectionRecords();
            for (int i = 0; i < connections.Length; i++)
                if (connections[i].r_IsActive != 0 || connections[i].r_RecordGlobalId != 0)
                    rows[$"connections/{i}/{connections[i].r_RecordGlobalId}"] = Fields(connections[i]);
        }

        private static void Grid<T>(IDictionary<string, string> rows, string category, Span<T> span) where T : struct
        {
            var value = new StringBuilder(2048);
            for (int start = 0; start < span.Length; start += 256)
            {
                value.Clear();
                for (int i = start; i < Math.Min(start + 256, span.Length); i++)
                    value.Append(span[i]).Append(',');
                rows[$"{category}/{start}/0"] = value.ToString();
            }
        }

        private static readonly Dictionary<Type, FieldInfo[]> fieldCache = new Dictionary<Type, FieldInfo[]>();
        private static string Fields<T>(T value) where T : struct
        {
            Type type = typeof(T);
            FieldInfo[] selected;
            lock (fieldCache)
            {
                if (!fieldCache.TryGetValue(type, out selected))
                {
                    var found = new List<FieldInfo>();
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
                        foreach (object attribute in field.GetCustomAttributes(false))
                            if (attribute.GetType().Name == "LuaExposedAttribute")
                            { found.Add(field); break; }
                    }
                    found.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
                    fieldCache[type] = selected = found.ToArray();
                }
            }
            object boxed = value;
            var result = new StringBuilder(selected.Length * 16);
            foreach (FieldInfo field in selected)
            {
                object data = field.GetValue(boxed);
                if (data is float f) data = BitConverter.ToInt32(BitConverter.GetBytes(f), 0);
                if (data is double d) data = BitConverter.DoubleToInt64Bits(d);
                result.Append(field.Name).Append('=').Append(data).Append(',');
            }
            return result.ToString();
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
            if (!incomplete)
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
