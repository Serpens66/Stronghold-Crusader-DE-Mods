using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
namespace FixesDiagnostics
{
    internal sealed unsafe class ObserverRuntime
    {
        private readonly ManualLogSource log;
        private readonly string owner;
        private readonly bool siege;
        private readonly IntPtr nativeModule;
        private readonly FixesContract fixes;
        // Publisher subscriptions are retained for the process lifetime.
        private readonly IDisposable spawnSubscription, buildSubscription;
        private readonly Dictionary<int, uint> buildings = new Dictionary<int, uint>();
        private readonly Dictionary<int, Dictionary<string, object>> lastPlayer = new Dictionary<int, Dictionary<string, object>>();
        private readonly Dictionary<int, string> lastPlayerJson = new Dictionary<int, string>();
        private string directory, output;
        private bool active, firstTick, callbackError, isolated;
        private int lastTick, exports, spawnCount;
        private bool published;
        private readonly Dictionary<int,int> buildingAliveStates = new Dictionary<int,int>();
        private long session;
        internal ObserverRuntime(ManualLogSource log, string owner, bool siege, IntPtr nativeModule)
        {
            this.log = log; this.owner = owner; this.siege = siege;
            if(nativeModule == IntPtr.Zero) throw new InvalidOperationException("Native module handle missing.");
            this.nativeModule = nativeModule;
            fixes = new FixesContract();
            ValidateLayout();
            spawnSubscription = BuildingR3EventHooks.OnBuildingSpawn.Observable.Subscribe(OnSpawn);
            try { buildSubscription = BuildingR3EventHooks.OnBuildStructure.Observable.Subscribe(OnBuild); }
            catch { spawnSubscription.Dispose(); throw; } // Unpublished candidate only.
        }
        internal void MarkPublished() => published = true;
        internal void RollbackUnpublished()
        {
            if(published) return;
            spawnSubscription.Dispose(); buildSubscription.Dispose();
        }
        private static void ValidateLayout()
        {
            if (Marshal.SizeOf<GameUnit>() != 0x490) throw new InvalidOperationException("GameUnit size changed.");
            foreach (var field in new[] { Tuple.Create("r_AliveState",0x88), Tuple.Create("r_ControllableForPlayerId",0x92),
                Tuple.Create("N0000019A",0x29c), Tuple.Create("r_TribeId",0x2d4), Tuple.Create("r_AIState",0x2bc), Tuple.Create("r_AITribeRole",0x426) })
                if (Marshal.OffsetOf<GameUnit>(field.Item1).ToInt32() != field.Item2) throw new InvalidOperationException("Crew layout changed: " + field.Item1);
        }
        internal void OnSessionStarted(Shared.GameplaySessionStartedContext context)
        {
            session = context.SessionId; active = false;
            bool observe = !context.IsEditor && !context.IsReplay;
            firstTick = false; callbackError = false; exports = spawnCount = 0;
            buildings.Clear(); buildingAliveStates.Clear(); lastPlayer.Clear(); lastPlayerJson.Clear();
            directory = Path.Combine(Paths.BepInExRootPath,"FixesEvidence",owner,DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + session);
            Directory.CreateDirectory(directory); output = Path.Combine(directory,"events.jsonl");
            var mods = Chainloader.PluginInfos.Values.Select(p => p.Metadata.GUID + "@" + p.Metadata.Version).OrderBy(p => p).ToArray();
            isolated = Chainloader.PluginInfos.Keys.All(id => id == "000shcdese" || id == "fixes" || id == "APIShared_Serp" || id == owner);
            string native = Path.Combine(Paths.GameRootPath,"Stronghold Crusader Definitive Edition_Data","Plugins","x86_64","CrusaderDE.dll");
            Record("SESSION",new Dictionary<string,object> { ["kind"] = context.Kind.ToString(), ["loadedSave"] = context.IsLoadedSave,
                ["file"] = context.SaveFileName, ["map"] = GamePlayerManagerAPI.Instance.GetCurrentMapName(),
                ["nativeSha256"] = Hash(File.ReadAllBytes(native)), ["mods"] = mods, ["isolated"] = isolated,
                ["fixtures"] = FixtureHashes(), ["activeFile"] = ActiveFileIdentity(), ["runtimeAicIdentities"] = RuntimeAicIdentities(), ["fixes124"] = fixes.ExactVersion, ["result"] = "INCONCLUSIVE", ["readOnly"] = true });
            active = observe && isolated && fixes.ExactVersion;
            if(observe && !active) Shared.DebugLogHelper.LogInfo(log,"FIXES_EVIDENCE_RECORDING_DISABLED: isolated=" + isolated + ",fixes124=" + fixes.ExactVersion);
        }
        internal void OnSessionEnded()
        {
            bool wasActive = active; active = false;
            if (wasActive) Record("RESULT",new Dictionary<string,object> { ["status"] = "INCONCLUSIVE", ["spawns"] = spawnCount,
                ["callbackError"] = callbackError, ["reason"] = "Gameplay effect and matched control require evidence review; absence is not a pass." });
            active = false;
        }
        internal void OnTick(int tick)
        {
            if (!active) return;
            try
            {
                if (output != null && new FileInfo(output).Length > 32 * 1024 * 1024) { OnSessionEnded(); return; }
                lastTick = tick;
                if (!firstTick) { firstTick = true; Record("POST_STARTUP_TICK",new Dictionary<string,object> { ["publisher"] = "GameTimeManagerAPI.OnTick" }); }
                for (int playerId = 1; playerId <= 8; playerId++)
                {
                    if (!GamePlayerManagerAPI.Instance.IsAIPlayer(playerId)) continue;
                    Dictionary<string,object> state = Player(playerId);
                    string json = Shared.DependencyFreeJson.Serialize(state);
                    if (!lastPlayerJson.TryGetValue(playerId,out string previous) || json != previous)
                    { Record("PLAYER", state); lastPlayerJson[playerId] = json; }
                    lastPlayer[playerId] = state;
                }
                // Keep identities stable across slot reuse; log transitions, including removal.
                foreach (var pair in buildings.ToArray())
                {
                    bool found = GameBuildingManagerAPI.Instance.TryGetBuildingById(pair.Key,out GameBuilding* b);
                    bool identity = found && b->r_GlobalId == pair.Value;
                    int alive = found ? (int)b->r_AliveState : -1;
                    if (!identity || alive != buildingAliveStates[pair.Key])
                    {
                        Record("BUILDING_TRANSITION",new Dictionary<string,object> { ["gameId"]=pair.Key,["globalId"]=pair.Value,
                            ["identityMatches"]=identity,["state"]=alive });
                        buildingAliveStates[pair.Key]=alive;
                    }
                    if(!identity || alive != 1 && alive != 2) { buildings.Remove(pair.Key); buildingAliveStates.Remove(pair.Key); }
                }
            }
            catch (Exception ex) { Error(ex); }
        }
        private Dictionary<string,object> Player(int playerId)
        {
            if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(playerId,out GamePlayerResources* p)) throw new InvalidOperationException("AI resources unavailable: " + playerId);
            var state = new Dictionary<string,object> { ["playerId"] = playerId, ["lord"] = GameAIManagerAPI.Instance.GetCustomAILordNameByPlayerId(playerId),
                ["lordEnum"] = (int)GamePlayerManagerAPI.Instance.GetAILord(playerId), ["preferences"] = fixes.Preferences(playerId),
                ["popularity"] = p->r_CurrentPopularity, ["badAttempts"] = p->r_BadThingBuildAttemptCounter,
                ["badEnabled"] = fixes.State("customBadThingLogicFlagArray",playerId),
                ["badMinimum"] = fixes.State("customBadThingRequiredMinPopularityArray",playerId),
                ["badRequiredAttempts"] = fixes.State("customBadThingRequiredAttemptsArray",playerId) };
            var aiv = GameAIVManagerAPI.Instance;
            if (aiv.TryGetVillageByPlayerId(playerId,out AivVillageState* village) && aiv.TryGetVillageSlotByPlayerId(playerId,out int villageSlot))
            {
                state["aivVariant"] = village->SelectedVariantIndex; state["aivUnlockedStep"] = village->UnlockedBuildStep;
                state["aivMaximumStep"] = village->MaximumBuildStep; state["aivBuildRate"] = village->BuildRate;
                state["aivLowGoldDelay"] = village->LowGoldBuildDelayElapsed;
                var badSteps = new List<object>(); Span<AivBuildStep> steps = aiv.GetBuildSteps(villageSlot);
                for(int i=0;i<steps.Length;i++)
                {
                    int mapper=(int)steps[i].BuildingType;
                    if(mapper==176 || mapper==177 || mapper>=301 && mapper<=311)
                        badSteps.Add(new Dictionary<string,object> { ["step"]=i,["mapper"]=mapper,["stateRaw"]=(int)steps[i].State,
                            ["tileCount"]=steps[i].TileCount,["tileRaw"]=steps[i].MapTileIdOrBufferIndex,["delay"]=steps[i].RebuildDelay });
                }
                state["aivBadSteps"] = badSteps;
            }
            if (!siege) return state;
            var aics = GameAIManagerAPI.Instance.GetAICArray(); int aicIndex=(int)GamePlayerManagerAPI.Instance.GetAILord(playerId);
            if(aicIndex>0 && aicIndex<aics.Length)
            {
                InternalAIC aic=aics.GetValue(aicIndex); int[] machines=new int[8];
                machines = new[] { aic.siege_machines1,aic.siege_machines2,aic.siege_machines3,aic.siege_machines4,
                    aic.siege_machines5,aic.siege_machines6,aic.siege_machines7,aic.siege_machines8 };
                state["machines"] = machines; state["triggerVariance"] = aic.siege_trigger_variance;
                state["jointAttackChance"] = aic.percent_chance_waiting_for_joint_attack; state["configuredEngineers"] = aic.siege_eng_amount;
            }
            state["siegeState"] = p->r_AISiegeState; state["targetPlayerId"] = p->r_SiegeAttackTargetPlayerId;
            state["rallyX"] = p->r_SiegeRallyTileX; state["rallyY"] = p->r_SiegeRallyTileY;
            state["engineerCounter"] = p->r_AICurrentSiegeEngineerCount;
            if (GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById((int)p->r_SiegeAttackTargetPlayerId,out GamePlayerResources* target))
            { state["targetKeepX"] = target->r_KeepDoorTilePositionX; state["targetKeepY"] = target->r_KeepDoorTilePositionY; }
            var crew = new List<object>(); var tribes = new Dictionary<int,object>(); var actualMachines = new List<object>(); int eligible = 0;
            Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            uint count = GameUnitManagerAPI.Instance.GetUnitManager().AsRef().r_TotalUnits;
            for (int spanIndex = 0; spanIndex < units.Length && spanIndex + 1 < count; spanIndex++)
            {
                ref GameUnit u = ref units[spanIndex];
                if(u.r_ControllableForPlayerId != playerId) continue;
                int chimp=(int)u.r_UnitChimp;
                if(chimp==39 || chimp==40 || chimp==58 || chimp==59 || chimp==60 || chimp==77)
                    actualMachines.Add(new Dictionary<string,object> { ["gameId"]=spanIndex+1,["globalId"]=u.r_GlobalId,["type"]=chimp,
                        ["alive"]=(int)u.r_AliveState,["x"]=u.r_CurrentTilePositionX,["y"]=u.r_CurrentTilePositionY,
                        ["engineer1"]=u.r_AssignedEngineer1,["engineer2"]=u.r_AssignedEngineer2,["health"]=u.r_CurrentHealth });
                if(chimp!=30) continue;
                if(u.r_TribeId!=0 && !tribes.ContainsKey(u.r_TribeId) && GameTribeManagerAPI.Instance.TryGetTribeById(u.r_TribeId,out GameTribe* tribe))
                    tribes[u.r_TribeId]=new Dictionary<string,object> { ["tribeId"]=u.r_TribeId,["globalId"]=tribe->r_GlobalId,
                        ["owner"]=tribe->r_PlayerIdOwner,["leaderUnitId"]=tribe->r_LeaderUnitId,["units"]=tribe->r_UnitsInGroup,["alive"]=(int)tribe->r_AliveState };
                // Exact six conjuncts from 0x2C1B0; native owner is a ushort, including its high byte.
                bool selected = (int)u.r_AliveState == 2 && u.N00000569 == 0 && (u.N0000019A & 0xffff) == 0 &&
                    u.r_AITribeRole == 10 && u.r_TribeId == 0 && u.r_AIState != 8;
                if (selected) eligible++;
                crew.Add(new Dictionary<string,object> { ["gameId"] = spanIndex + 1, ["globalId"] = u.r_GlobalId, ["eligibleNow"] = selected,
                    ["alive"] = (int)u.r_AliveState, ["x"] = u.r_CurrentTilePositionX, ["y"] = u.r_CurrentTilePositionY,
                    ["targetX"] = u.r_TargetTilePositionX, ["targetY"] = u.r_TargetTilePositionY, ["tribeId"] = u.r_TribeId,
                    ["nativeEligibilityWord"] = u.N0000019A & 0xffff, ["ownerHighByte"] = u.N00000569, ["role"] = u.r_AITribeRole, ["aiState"] = u.r_AIState, ["command"] = u.r_AI_LastIssuedTribeCommand,
                    ["pathState"] = u.r_PathPlanStateBitFlags, ["health"] = u.r_CurrentHealth });
            }
            state["eligibleNow"] = eligible; state["crew"] = crew; state["tribes"] = tribes.Values.ToList(); state["actualMachines"] = actualMachines;
            state["eligibleTiming"] = "tick sample; producer detaches siege tribe before selection; absence here is inconclusive";
            return state;
        }
        private static bool Tent(eStructs type) => type == eStructs.STRUCT_SIEGE_TENT || type == eStructs.STRUCT_SIEGE_TENT_ARAB_BALLISTA ||
            type >= eStructs.STRUCT_SIEGE_TENT_CATAPULT && type <= eStructs.STRUCT_SIEGE_TENT_PORTABLE_SHIELD;
        private static bool Bad(eStructs type) => type == eStructs.STRUCT_GALLOWS || type == eStructs.STRUCT_STOCKS || type == eStructs.STRUCT_WITCH_HOIST ||
            type == eStructs.STRUCT_BURNING_STAKE || type == eStructs.STRUCT_GIBBET || type == eStructs.STRUCT_DUNGEON ||
            type == eStructs.STRUCT_CESS_PIT || type == eStructs.STRUCT_RACK_STRETCHING || type == eStructs.STRUCT_RACK_FLOGGING || type == eStructs.STRUCT_CHOPPING_BLOCK;
        private void OnSpawn(BuildingSpawnEventArgs e)
        {
            if (!active || e.Phase != EventHookPhase.Post || !(siege ? Tent(e.Building) : Bad(e.Building))) return;
            try
            {
                int id = checked((int)e.ReturnValue);
                if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(id,out GameBuilding* b) || b->r_GlobalId == 0 || b->r_BuildingType != e.Building || b->r_PlayerIdOwner != e.PlayerId)
                    throw new InvalidOperationException("Spawn identity mismatch: " + id);
                if (buildings.TryGetValue(id,out uint global) && global == b->r_GlobalId) return;
                buildings[id] = b->r_GlobalId; buildingAliveStates[id]=(int)b->r_AliveState; spawnCount++;
                Record("SPAWN",new Dictionary<string,object> { ["gameId"] = id, ["globalId"] = b->r_GlobalId, ["type"] = e.Building.ToString(),
                    ["alive"] = (int)b->r_AliveState, ["x"] = b->r_TilePositionXBegin, ["y"] = b->r_TilePositionYBegin,
                    ["player"] = Player(e.PlayerId), ["previousTickPlayer"] = lastPlayer.TryGetValue(e.PlayerId,out var prior) ? prior : null,
                    ["result"] = "INCONCLUSIVE", ["reason"] = "Natural AI/AIV origin, prerequisites and control must be verified." });
            }
            catch(Exception ex) { Error(ex); }
        }
        private void OnBuild(BuildStructureEventArgs e)
        {
            if (!active || e.Phase != EventHookPhase.Pre) return;
            int mapper = (int)e.Mappers;
            if (!(siege ? mapper >= 190 && mapper <= 194 || mapper == 358 : mapper == 176 || mapper == 177 || mapper >= 301 && mapper <= 311)) return;
            try
            {
                Record("BUILD_REQUEST", new Dictionary<string,object> { ["playerId"] = e.PlayerId, ["mapper"] = mapper, ["x"] = e.TileX,
                    ["y"] = e.TileY, ["free"] = e.IsFree, ["player"] = Player(e.PlayerId) });
                if (siege && exports < 4) { ExportGeometry(e); exports++; }
            }
            catch(Exception ex) { Error(ex); }
        }
        private void ExportGeometry(BuildStructureEventArgs e)
        {
            var tiles = GameTileManagerAPI.Instance; var view = tiles.TileManager;
            int[] rows = new int[2400]; ushort[] columns = new ushort[320800];
            for(int i=0;i<rows.Length;i++) rows[i] = tiles.MapRowLookupTable[i];
            for(int i=0;i<columns.Length;i++) columns[i] = tiles.MapColumnLookupTable[i];
            var tents = new List<object>(); Span<GameBuilding> all = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            for(int spanIndex=0;spanIndex<all.Length;spanIndex++)
            {
                ref GameBuilding b = ref all[spanIndex];
                if ((int)b.r_AliveState == 2 && Tent(b.r_BuildingType)) tents.Add(new Dictionary<string,object> { ["id"] = spanIndex+1,
                    ["globalId"] = b.r_GlobalId, ["owner"] = b.r_PlayerIdOwner, ["type"] = (int)b.r_BuildingType,
                    ["x"] = b.r_TilePositionXBegin, ["y"] = b.r_TilePositionYBegin });
            }
            var module = nativeModule;
            int selector = Marshal.ReadInt32(module + checked(0x2EA70DC + e.PlayerId * 0x177BC));
            if (selector < 0 || selector > 8) throw new InvalidOperationException("Raw native metric selector out of bounds: " + selector);
            short[] metric = new short[320800];
            Marshal.Copy(module + checked(0x5759230 + selector * 320800 * 2),metric,0,metric.Length);
            byte[] treeAllowed = new byte[320800];
            if (Marshal.SizeOf<GameVegetation>() != 156 || Marshal.OffsetOf<GameVegetationManager>("VegetationArray").ToInt32() != 0x1A ||
                Marshal.OffsetOf<GameVegetation>("r_AliveState").ToInt32() != 0x50) throw new InvalidOperationException("Vegetation layout changed.");
            GameVegetationManager* vegetation = GameVegetationManagerAPI.Instance.GetVegetationManager();
            Span<ushort> organisms = view.OrganismGrid;
            for(int i=0;i<treeAllowed.Length;i++)
            {
                int id = organisms[i];
                if (id >= 5000) throw new InvalidOperationException("Vegetation slot outside capacity: " + id);
                short value = id == 0 ? (short)0 : *(short*)((byte*)vegetation + id * 156 + 0x6A);
                treeAllowed[i] = (byte)(value > 4 && value != 15 ? 1 : 0);
            }
            byte[] activeCoordinates = new byte[640000];
            // Hash-bound read of the native active-coordinate table; no executable memory mutation.
            Marshal.Copy(nativeModule + 0x3A11EA4,activeCoordinates,0,activeCoordinates.Length);
            var data = new Dictionary<string,object> { ["nativeHash"] = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2",
                ["rows"] = rows, ["columns"] = columns, ["neighbors"] = tiles.GetPackedNeighborTileDeltas().ToArray(),
                ["edges"] = view.PathEdgeMaskGrid.ToArray(), ["logic"] = view.LogicGrid.ToArray(), ["structures"] = view.StructureGrid.ToArray(),
                ["units"] = view.TileUnitIdGrid.ToArray(), ["heights"] = view.HeightGrid.ToArray(), ["pcl"] = view.PathConnectionGrid.ToArray(),
                ["metricSelectorRaw"] = selector, ["metric"] = metric, ["treeAllowed"] = treeAllowed, ["activeCoordinates"] = activeCoordinates, ["tents"] = tents, ["player"] = Player(e.PlayerId),
                ["observedOriginX"] = e.TileX, ["observedOriginY"] = e.TileY, ["tick"] = lastTick,
                ["capturePhase"] = "BuildStructure.Pre after native search; crew leader must be identified before model validation" };
            File.WriteAllText(Path.Combine(directory,"geometry-" + exports + ".json"),Shared.DependencyFreeJson.Serialize(data),new UTF8Encoding(false));
        }
        private void Error(Exception ex)
        {
            if (callbackError) return; callbackError = true;
            try { Shared.DebugLogHelper.LogError(log,"FIXES_EVIDENCE_CALLBACK_ERROR: " + ex); } catch { }
            try { if (output != null) Record("CALLBACK_ERROR",new Dictionary<string,object> { ["error"] = ex.ToString(), ["result"] = "INCONCLUSIVE" }); } catch { }

        }
        private void Record(string marker, Dictionary<string,object> data)
        {
            data["marker"] = marker; data["utc"] = DateTime.UtcNow.ToString("O"); data["session"] = session; data["tick"] = lastTick;
            // One JSON object per line, through the dependency-free codec.
            string json = Compact(Shared.DependencyFreeJson.Serialize(data));
            File.AppendAllText(output,json + Environment.NewLine,new UTF8Encoding(false));
            if (marker != "PLAYER") Shared.DebugLogHelper.LogInfo(log,"FIXES_EVIDENCE_" + marker + ": session=" + session + ",tick=" + lastTick + ",file=" + output);
        }
        private static string Compact(string json)
        {
            var result = new StringBuilder(json.Length); bool quoted = false, escaped = false;
            foreach(char c in json)
            {
                if (quoted) { result.Append(c); if(escaped) escaped=false; else if(c=='\\') escaped=true; else if(c=='"') quoted=false; }
                else if(c=='"') { quoted=true; result.Append(c); }
                else if(!char.IsWhiteSpace(c)) result.Append(c);
            }
            return result.ToString();
        }
        private static Dictionary<string,object> ActiveFileIdentity()
        {
            string path=GameMapArchiveManagerAPI.Instance.GetCurrentFilePath();
            return new Dictionary<string,object>{["path"]=path,["isSave"]=GameMapArchiveManagerAPI.Instance.IsCurrentFileSaveFile(),
                ["sha256"]=string.IsNullOrEmpty(path) || !File.Exists(path) ? null : Hash(File.ReadAllBytes(path)),
                ["resolved"]=!string.IsNullOrEmpty(path) && File.Exists(path)};
        }
        private static List<object> RuntimeAicIdentities()
        {
            var result=new List<object>(); var aics=GameAIManagerAPI.Instance.GetAICArray();
            for(int playerId=1;playerId<=8;playerId++)
            {
                if(!GamePlayerManagerAPI.Instance.IsAIPlayer(playerId))continue;
                int index=(int)GamePlayerManagerAPI.Instance.GetAILord(playerId);
                if(index<1 || index>=aics.Length)continue;
                object aic=aics.GetValue(index);
                var values=typeof(InternalAIC).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                    .OrderBy(f=>f.Name,StringComparer.Ordinal).ToDictionary(f=>f.Name,f=>f.GetValue(aic));
                string json=Shared.DependencyFreeJson.Serialize(values);
                result.Add(new Dictionary<string,object>{["playerId"]=playerId,["lordEnum"]=index,
                    ["lordName"]=GameAIManagerAPI.Instance.GetCustomAILordNameByPlayerId(playerId),
                    ["effectiveAicSha256"]=Hash(Encoding.UTF8.GetBytes(json)),["effectiveAic"]=values,
                    ["sourcePackageConfirmed"]=false,["note"]="Effective native values; bind editor package/AIV files independently in evidence review."});
            }
            return result;
        }
        private static Dictionary<string,object> FixtureHashes()
        {
            string root=Path.Combine(Path.GetDirectoryName(typeof(ObserverRuntime).Assembly.Location),"Fixtures");
            return Directory.Exists(root) ? Directory.GetFiles(root,"*",SearchOption.AllDirectories)
                .ToDictionary(path=>path.Substring(root.Length+1),path=>(object)Hash(File.ReadAllBytes(path))) : new Dictionary<string,object>();
        }
        private static string Hash(byte[] bytes) { using(var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-",string.Empty); }
    }
}
