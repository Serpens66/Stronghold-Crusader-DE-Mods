using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using APIShared;
using APIShared.Simulation;
using CrusaderDE;
using RedBird.Core.Memory;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace UiAcceptance
{
    // Passive observer for manually played sessions: never changes game state. Every intervalTicks map ticks the
    // simulation callback logs one OBSERVE line, read under the engine monitor at the same native map tick on every
    // peer, so the text after "label=" is identical on host and client while they simulate the same world.
    // The registration lives for the process (SimulationTickEvents has no unregistration) and covers every later
    // mission, including loaded saves. The step itself never completes; the user ends the run by quitting the game.
    internal static unsafe class ObserveSteps
    {
        private const int MaxPlayers = 8;
        // Vanilla's gate closure (0xC4BF0, DRAWBRIDGE_REGION_CLOSURE.md, confirmed) handles exactly the gatehouse types
        // 0x2D..0x2F and treats the native WORD at Interop r_GateState/r_GateState2 (0x2A2) as closed when it is 2.
        private const int ClosedGateState = 2;
        // ExtraFeatures' gatehouse set; its manual (not automatic) gates carry r_GateDoNotCloseForTicks == -1.
        private static readonly eStructs[] AutomationTypes =
        {
            eStructs.STRUCT_GATE_MAIN, eStructs.STRUCT_GATE_INNER, eStructs.STRUCT_GATE_WOOD,
            eStructs.STRUCT_GATE_POSTERN, eStructs.STRUCT_GATEHOUSE, eStructs.STRUCT_DRAWBRIDGE
        };

        private static bool registered;
        private static int interval;            // 0 until the step armed the observer
        private static long labelSession = -1;  // simulation thread only
        private static string label;
        private static long missionLogged = -1; // Unity thread only

        internal static void Register()
        {
            Runner.Register("Observe", Observe);
        }

        // args: intervalTicks. Runs for the rest of the process; give it a timeoutMs that is never reached.
        private static IEnumerable<Wait> Observe(Step step)
        {
            int ticks = step.Int(0);
            Runner.Require(ticks >= 1, "the observe interval must be at least one map tick");
            int armed = Volatile.Read(ref interval);
            Runner.Require(armed == 0 || armed == ticks, "the observer already runs every " + armed + " map ticks");
            HuntSteps.Engine(); // resolve the engine monitor on the Unity thread before the simulation callback needs it
            if (!registered)
            {
                // Registered from the main menu, before any mission, so every peer has the same callback set.
                Runner.Require(SimulationTickEvents.TryRegister(UiAcceptancePlugin.Guid, "observe", OnTick, out string reason, 1000), reason);
                registered = true;
            }
            Volatile.Write(ref interval, ticks);
            AcceptLog.Info("OBSERVE_ARMED run=" + AcceptLog.RunId + " interval=" + ticks);
            yield return new Until("the end of the process (passive observation)", () =>
            {
                LogMission();
                return false;
            });
        }

        // ---- simulation thread ----

        private static void OnTick(int tick)
        {
            int every = Volatile.Read(ref interval);
            if (every <= 0 || tick % every != 0) return;
            MissionContext mission = UiAcceptancePlugin.Missions?.Current;
            if (mission == null) return;
            string payload;
            try
            {
                lock (HuntSteps.Engine()) payload = Snapshot();
            }
            catch (Exception error)
            {
                // Observation only: a failed read is reported and never stops the run.
                AcceptLog.Warn("OBSERVE_FAILED run=" + AcceptLog.RunId + " session=" + mission.SessionId + " tick=" + tick + " " +
                    error.GetType().Name + ": " + error.Message);
                return;
            }
            if (mission.SessionId != labelSession)
            {
                label = Label(mission);
                labelSession = mission.SessionId;
            }
            AcceptLog.Info("OBSERVE run=" + AcceptLog.RunId + " session=" + mission.SessionId + " tick=" + tick +
                " label=" + label + " " + payload);
        }

        // Peer-independent mission label: how the mission started and its map tick at NativeLoaded (the save's own
        // tick for a loaded save, see MISSION_ANCHOR).
        private static string Label(MissionContext mission)
        {
            string kind = mission.IsSave ? "save" : "new";
            bool anchored = Interlocked.Read(ref UiAcceptancePlugin.AnchorSession) == mission.SessionId;
            return kind + "-" + (anchored ? Volatile.Read(ref UiAcceptancePlugin.AnchorTick).ToString(CultureInfo.InvariantCulture) : "noanchor");
        }

        // Everything here depends only on the simulated world, never on the local peer.
        private static string Snapshot()
        {
            var buildings = new int[MaxPlayers + 1];
            var quarries = new int[MaxPlayers + 1];
            var piles = new int[MaxPlayers + 1];
            var linked = new int[MaxPlayers + 1];
            var units = new int[MaxPlayers + 1];
            var gateStates = new SortedDictionary<int, int>();
            int gates = 0, open = 0, bridges = 0, manual = 0;
            ulong hash = 14695981039346656037UL;

            SimpleNativeArray<GameBuilding> array = GameBuildingManagerAPI.Instance.GetBuildingsArray();
            for (int index = 0; index < array.Length; index++)
            {
                GameBuilding* building = array.GetValuePointer(index);
                if (building->r_AliveState == AliveState.None) continue;
                int owner = building->r_PlayerIdOwner;
                eStructs type = building->r_BuildingType;
                hash = Mix(hash, (uint)(index + 1)); // building ID = slot index + 1
                hash = Mix(hash, (uint)type);
                hash = Mix(hash, (uint)owner);
                hash = Mix(hash, building->r_TilePositionXBegin);
                hash = Mix(hash, building->r_TilePositionYBegin);
                hash = Mix(hash, (uint)building->r_CurrentHealth);
                bool player = owner >= 1 && owner <= MaxPlayers;
                if (player) buildings[owner]++;
                if (type == eStructs.STRUCT_QUARRYPILE && player) piles[owner]++;
                else if (type == eStructs.STRUCT_QUARRY && player)
                {
                    quarries[owner]++;
                    if (HasLinkedPile(building, owner)) linked[owner]++;
                }
                if (Array.IndexOf(AutomationTypes, type) < 0) continue;
                int state = building->r_GateState | building->r_GateState2 << 8;
                gateStates.TryGetValue(state, out int seen);
                gateStates[state] = seen + 1;
                if (building->r_GateDoNotCloseForTicks == -1) manual++;
                if (type == eStructs.STRUCT_DRAWBRIDGE) bridges++;
                else if (type == eStructs.STRUCT_GATE_MAIN || type == eStructs.STRUCT_GATE_INNER || type == eStructs.STRUCT_GATE_WOOD)
                {
                    gates++;
                    if (state != ClosedGateState) open++;
                }
            }

            var unitsByOwner = new int[MaxPlayers + 1]; // includes player 0 (animals, neutral units) for the hash
            SimpleNativeArray<GameUnit> unitArray = GameUnitManagerAPI.Instance.GetUnitArray();
            for (int index = 0; index < unitArray.Length; index++)
            {
                GameUnit* unit = unitArray.GetValuePointer(index);
                if (!UnitAccess.IsReallyAlive(unit)) continue;
                int owner = unit->r_ControllableForPlayerId;
                if (owner >= 0 && owner <= MaxPlayers) unitsByOwner[owner]++;
            }
            for (int owner = 0; owner <= MaxPlayers; owner++)
            {
                hash = Mix(hash, (uint)unitsByOwner[owner]);
                if (owner >= 1) units[owner] = unitsByOwner[owner];
            }

            var text = new StringBuilder(512);
            text.Append("buildings=").Append(PerPlayer(buildings));
            text.Append(" quarries=").Append(PerPlayer(quarries));
            text.Append(" piles=").Append(PerPlayer(piles));
            text.Append(" linked=").Append(PerPlayer(linked));
            text.Append(" gates=").Append(gates).Append('/').Append(open);
            text.Append(" bridges=").Append(bridges);
            text.Append(" manual=").Append(manual);
            text.Append(" gateRaw=");
            if (gateStates.Count == 0) text.Append('-');
            bool first = true;
            foreach (var entry in gateStates)
            {
                if (!first) text.Append(',');
                first = false;
                text.Append(entry.Key.ToString(CultureInfo.InvariantCulture)).Append(':').Append(entry.Value.ToString(CultureInfo.InvariantCulture));
            }
            text.Append(" units=").Append(PerPlayer(units));
            text.Append(" hash=").Append(hash.ToString("X16", CultureInfo.InvariantCulture));
            return text.ToString();
        }

        // Same ownership and type contract as BugfixesAndQoL NativeQuarryPlacement.HasLinkedPile (without its
        // placement-position check, which only identifies a freshly placed pair).
        private static bool HasLinkedPile(GameBuilding* quarry, int owner)
        {
            int pileId = quarry->r_StoneQuarry_StockPileBuildingId;
            return pileId != 0 && BuildingAccess.TryGetById(pileId, out GameBuilding* pile, out _) && pile != null &&
                pile->r_AliveState != AliveState.None && pile->r_BuildingType == eStructs.STRUCT_QUARRYPILE &&
                pile->r_PlayerIdOwner == owner;
        }

        private static string PerPlayer(int[] values)
        {
            var text = new StringBuilder(64);
            for (int player = 1; player <= MaxPlayers; player++)
            {
                if (player > 1) text.Append(',');
                text.Append(player.ToString(CultureInfo.InvariantCulture)).Append(':').Append(values[player].ToString(CultureInfo.InvariantCulture));
            }
            return text.ToString();
        }

        // FNV-1a over the little-endian bytes, as in HuntMpSteps.
        private static ulong Mix(ulong hash, uint value)
        {
            for (int shift = 0; shift < 32; shift += 8)
            {
                hash ^= (value >> shift) & 0xFF;
                hash *= 1099511628211UL;
            }
            return hash;
        }

        // ---- Unity thread ----

        // Once per mission, as soon as the mission lifecycle reports it ready.
        private static void LogMission()
        {
            MissionContext mission = UiAcceptancePlugin.Missions?.Current;
            if (mission == null || mission.SessionId == missionLogged) return;
            missionLogged = mission.SessionId;
            string players;
            try
            {
                lock (HuntSteps.Engine()) players = Players();
            }
            catch (Exception error)
            {
                players = "unavailable(" + error.GetType().Name + ")";
            }
            AcceptLog.Info("OBSERVE_MISSION run=" + AcceptLog.RunId + " session=" + mission.SessionId + " label=" + Label(mission) +
                " save=" + mission.IsSave + " players=" + players + " mode=" + mission.Mode.ToDiagnosticString());
        }

        // <id>:<lord>/<AI|human>, comma-separated. Lord: Vanilla's AI lord of the slot; humans have "-".
        // Sources: Vanilla's MP game member list, else the native player table (single player).
        private static string Players()
        {
            var members = Platform_Multiplayer.Instance?.gameMembers;
            var players = GamePlayerManagerAPI.Instance;
            var text = new StringBuilder(128);
            for (int id = 1; id <= MaxPlayers; id++)
            {
                string entry = null;
                if (members != null)
                    foreach (var member in members)
                    {
                        if (member == null || member.kicked || member.playerID != id) continue;
                        entry = member.skirmishAI ? players.GetAILord(id) + "/AI" : "-/human";
                        break;
                    }
                if (entry == null && players.GetLordUnitId(id) > 0)
                    entry = players.IsAIPlayer(id) ? players.GetAILord(id) + "/AI" : "-/human";
                if (entry == null) continue;
                if (text.Length > 0) text.Append(',');
                text.Append(id.ToString(CultureInfo.InvariantCulture)).Append(':').Append(entry.Replace(' ', '_').Replace(',', '_'));
            }
            return (text.Length > 0 ? text.ToString() : "-") + " customLords=" + CustomLords();
        }

        // Vanilla's in-game member list has no custom lord name; the lobby (still held during an MP mission) does.
        // Names only: the lobby member carries no verified player ID, so they are not assigned to a slot.
        private static string CustomLords()
        {
            var members = MainViewModel.viewModelLoaded ? MainViewModel.Instance.FRONTMultiplayer?.currentLobby?.members : null;
            if (members == null) return "-";
            var names = new List<string>();
            foreach (var member in members)
                if (member != null && !string.IsNullOrEmpty(member.customLordName)) names.Add(member.customLordName.Replace(' ', '_').Replace(',', '_'));
            return names.Count > 0 ? string.Join(",", names) : "-";
        }
    }
}
