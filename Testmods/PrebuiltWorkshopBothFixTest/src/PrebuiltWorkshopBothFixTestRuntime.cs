using APIShared;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using AILords = Enums.AILords;

namespace PrebuiltWorkshopBothFixTest
{
    internal sealed unsafe class PrebuiltWorkshopBothFixTestRuntime
    {
        private const int PrebuiltReferenceRva = 0x95FF8;
        private const int Both = -999;
        private const string PrebuiltReferencePattern =
            "8B 0D ?? ?? ?? ?? 8D 46 FF 0F AB C1 41 B8 64 00 00 00 " +
            "89 0D ?? ?? ?? ?? 8B D6 49 8B CD E8 ?? ?? ?? ??";

        private readonly ManualLogSource log;
        private readonly IntPtr prebuiltPlayersBits;
        private readonly Dictionary<int, SpawnObservation> observed = new Dictionary<int, SpawnObservation>();
        private bool captureNativeStart;
        private bool sessionStarted;
        private bool eligibleSession;
        private bool attempted;
        private bool loggedFirstTick;
        private int pendingTicks;

        internal PrebuiltWorkshopBothFixTestRuntime(ManualLogSource log, CrusaderLibraryLoadContext context)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            if (context == null) throw new ArgumentNullException(nameof(context));
            ReadOnlySpan<byte> memory = context.Memory;
            if (!Shared.NativePatternResolver.MatchesPatternAt(memory, PrebuiltReferenceRva, PrebuiltReferencePattern))
                throw new InvalidOperationException("Prebuilt-player bitfield reference differs from the audited native build.");
            int targetRva = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory, PrebuiltReferenceRva + 2, PrebuiltReferenceRva + 6);
            if (targetRva < 0 || targetRva > memory.Length - sizeof(int))
                throw new InvalidOperationException("Prebuilt-player bitfield target is outside the native image.");
            prebuiltPlayersBits = IntPtr.Add(context.ModuleHandle, targetRva);
            ValidateInteropLayout();
            Shared.DebugLogHelper.LogInfo(log,
                $"Read-only prebuilt-player bitfield validated: reference=0x{PrebuiltReferenceRva:X}, target=0x{targetRva:X}.");
        }

        internal void OnNativeStart(MissionLifecycleNotification notification)
        {
            try
            {
                if (notification.IsBeforeInitialization)
                {
                    observed.Clear();
                    captureNativeStart = !notification.Context.IsSave && !notification.Context.IsEditor;
                    sessionStarted = false;
                    eligibleSession = false;
                    attempted = false;
                    loggedFirstTick = false;
                    pendingTicks = 0;
                    Shared.DebugLogHelper.LogInfo(log,
                        $"Native map start: capture={captureNativeStart}, save={notification.Context.IsSave}, editor={notification.Context.IsEditor}.");
                }
                else
                {
                    captureNativeStart = false;
                    Shared.DebugLogHelper.LogInfo(log,
                        $"Native map start completed: observedPrebuiltWorkshops={observed.Count}, prebuiltBits=0x{ReadPrebuiltBits():X8}.");
                    LogObservedWorkshops("native-end");
                }
            }
            catch (Exception ex) { Fail("Native-start observation failed", ex); }
        }

        internal void OnBuildingSpawn(BuildingSpawnEventArgs args)
        {
            if (!captureNativeStart || !IsWorkshop(args.Building) || args.PlayerId < 1 || args.PlayerId > 8)
                return;
            try
            {
                int id = checked((int)args.ReturnValue);
                if (id <= 0 || (ReadPrebuiltBits() & (1 << (args.PlayerId - 1))) == 0)
                    return;
                if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(args.PlayerId, out GamePlayerResources* player))
                    return;
                int? aicAtSpawn = ReadAicSetting(args.PlayerId, args.Building);
                var item = new SpawnObservation(id, args.PlayerId, args.Building, player->r_AILordMinusOne, aicAtSpawn);
                observed[id] = item;
                Shared.DebugLogHelper.LogInfo(log,
                    $"Prebuilt workshop spawned: player={item.PlayerId}, id={id}, type={item.Type}, nativeLordSlot={item.LordSlotAtSpawn}, rosterLord={GamePlayerManagerAPI.Instance.GetAILord(item.PlayerId)}, aicValue={item.AicAtSpawn}.");
                LogWorkshop(item, "spawn-post");
            }
            catch (Exception ex) { Fail("Workshop spawn observation failed", ex); }
        }

        internal void OnSessionStarted(Shared.GameplaySessionStartedContext session)
        {
            eligibleSession = session.Kind == Shared.GameplaySessionStartKind.NewMap &&
                !session.IsLoadedSave && !session.IsEditor && !session.IsReplay;
            captureNativeStart = false;
            Shared.DebugLogHelper.LogInfo(log,
                $"Managed mission start: kind={session.Kind}, session={session.SessionId}, prebuiltBits=0x{ReadPrebuiltBits():X8}, observations={observed.Count}.");
            try
            {
                LogPrebuiltPlayersAndWorkshops();
                LogObservedWorkshops("managed-start");
                if (eligibleSession)
                {
                    if (observed.Count == 0)
                    {
                        attempted = true;
                        Shared.DebugLogHelper.LogInfo(log,
                            "No prebuilt workshop was observed during native map start; no correction is applicable.");
                    }
                    else DiagnoseAndCorrect("managed-start");
                }
                else
                    Shared.DebugLogHelper.LogInfo(log, "Correction excluded: save, editor, or replay session.");
            }
            catch (Exception ex) { Fail("Managed-start diagnosis failed", ex); }
            finally { sessionStarted = true; }
        }

        internal void OnTick(int tick)
        {
            if (!sessionStarted) return;
            if (!loggedFirstTick)
            {
                loggedFirstTick = true;
                Shared.DebugLogHelper.LogInfo(log, $"Post-startup-cleanup runtime marker: firstSimulationTick={tick}, attempted={attempted}.");
                try { LogObservedWorkshops("first-tick"); }
                catch (Exception ex) { Fail("First-tick workshop snapshot failed", ex); }
            }
            if (!eligibleSession || attempted) return;
            pendingTicks++;
            try { DiagnoseAndCorrect("simulation-tick"); }
            catch (Exception ex) { Fail("Early-tick diagnosis failed", ex); }
            if (!attempted && pendingTicks >= 128)
            {
                attempted = true;
                Shared.DebugLogHelper.LogWarning(log,
                    "Lord assignment remained invalid through 128 simulation ticks; no production values were changed for pending workshops.");
            }
        }

        internal void OnSessionEnded()
        {
            captureNativeStart = false;
            sessionStarted = false;
            eligibleSession = false;
            observed.Clear();
            pendingTicks = 0;
        }

        private void DiagnoseAndCorrect(string phase)
        {
            if (attempted || observed.Count == 0) return;
            var groups = observed.Values.GroupBy(x => new { x.PlayerId, x.Type })
                .OrderBy(g => g.Key.PlayerId).ThenBy(g => (int)g.Key.Type).ToArray();
            bool waitingForLord = false;
            foreach (var group in groups)
            {
                int playerId = group.Key.PlayerId;
                var items = group.OrderBy(x => x.Id).ToArray();
                if (items.Any(x => x.AicAtSpawn != Both))
                {
                    Shared.DebugLogHelper.LogInfo(log,
                        $"Workshop group excluded: player={playerId}, type={group.Key.Type}, phase={phase}; AIC was not Both at every spawn.");
                    continue;
                }
                if ((ReadPrebuiltBits() & (1 << (playerId - 1))) == 0)
                {
                    Shared.DebugLogHelper.LogWarning(log,
                        $"Workshop group excluded: player={playerId}, type={group.Key.Type}, phase={phase}; prebuilt-player bit is no longer set.");
                    continue;
                }
                if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(playerId, out GamePlayerResources* player))
                    continue;
                AILords lord = GamePlayerManagerAPI.Instance.GetAILord(playerId);
                if (player->r_AILordMinusOne == 0 || lord == AILords.SK_NULL)
                {
                    waitingForLord = true;
                    if (phase == "managed-start" || pendingTicks == 1 || pendingTicks == 128)
                        Shared.DebugLogHelper.LogInfo(log, $"Waiting for lord assignment: player={playerId}, type={group.Key.Type}, phase={phase}, nativeLordSlot={player->r_AILordMinusOne}, rosterLord={lord}.");
                    continue;
                }
                var aics = GameAIManagerAPI.Instance.GetAICArray();
                int aicIndex = (int)lord;
                if (aicIndex <= 0 || aicIndex >= aics.Length)
                    continue;
                InternalAIC aic = aics[aicIndex];
                int setting = GetAicSetting(aic, group.Key.Type);
                Shared.DebugLogHelper.LogInfo(log,
                    $"AIC workshop decision: player={playerId}, lord={lord}, nativeLordSlot={player->r_AILordMinusOne}, type={group.Key.Type}, aicValue={setting}, phase={phase}.");
                if (setting != Both) continue;

                var buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
                var ids = new HashSet<int>(items.Select(x => x.Id));
                var pair = GetGoods(group.Key.Type);
                int firstCount = 0, secondCount = 0;
                for (int spanIndex = 0; spanIndex < buildings.Length; spanIndex++)
                {
                    ref GameBuilding other = ref buildings[spanIndex];
                    if (other.r_AliveState != AliveState.IsAlive || other.r_PlayerIdOwner != playerId ||
                        other.r_BuildingType != group.Key.Type || ids.Contains(spanIndex + 1)) continue;
                    CountAsVanilla(group.Key.Type, other.r_NextProducedGoodId, pair.Item1,
                        ref firstCount, ref secondCount);
                }

                var changes = new List<Tuple<int, eGoods>>();
                eGoods vanillaDefault = group.Key.Type == eStructs.STRUCT_BLACKSMITHS_WORKSHOP
                    ? pair.Item2 : pair.Item1;
                foreach (SpawnObservation item in items)
                {
                    if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(item.Id, out GameBuilding* building) ||
                        building->r_AliveState != AliveState.NeedsInit || building->r_PlayerIdOwner != playerId ||
                        building->r_BuildingType != group.Key.Type ||
                        building->r_NextProducedGoodId != vanillaDefault ||
                        building->r_ProducedGoodId != vanillaDefault)
                    {
                        Shared.DebugLogHelper.LogWarning(log,
                            $"Workshop group differs from the confirmed NeedsInit/default-goods failure: player={playerId}, type={group.Key.Type}, id={item.Id}; no values changed.");
                        changes.Clear();
                        break;
                    }
                    eGoods desired = ChooseVanillaMinority(group.Key.Type, pair.Item1, pair.Item2,
                        firstCount, secondCount);
                    Shared.DebugLogHelper.LogInfo(log,
                        $"Workshop before correction: player={playerId}, id={item.Id}, type={group.Key.Type}, alive={building->r_AliveState}, next={building->r_NextProducedGoodId}, produced={building->r_ProducedGoodId}, vanillaChoice={desired}, priorCounts={firstCount}/{secondCount}.");
                    changes.Add(Tuple.Create(item.Id, desired));
                    CountAsVanilla(group.Key.Type, desired, pair.Item1, ref firstCount, ref secondCount);
                }
                if (changes.Count != items.Length) continue;
                bool mismatch = changes.Any(change =>
                {
                    GameBuildingManagerAPI.Instance.TryGetBuildingById(change.Item1, out GameBuilding* b);
                    return b->r_NextProducedGoodId != change.Item2 || b->r_ProducedGoodId != change.Item2;
                });
                if (!mismatch)
                {
                    Shared.DebugLogHelper.LogInfo(log, $"Vanilla production already matches Both: player={playerId}, type={group.Key.Type}; no change.");
                    continue;
                }
                foreach (var change in changes)
                {
                    GameBuildingManagerAPI.Instance.TryGetBuildingById(change.Item1, out GameBuilding* building);
                    building->r_NextProducedGoodId = change.Item2;
                    building->r_ProducedGoodId = change.Item2;
                    Shared.DebugLogHelper.LogInfo(log,
                        $"Workshop corrected: player={playerId}, id={change.Item1}, type={group.Key.Type}, next={building->r_NextProducedGoodId}, produced={building->r_ProducedGoodId}.");
                }
            }
            attempted = !waitingForLord;
        }

        private void LogPrebuiltPlayersAndWorkshops()
        {
            int bits = ReadPrebuiltBits();
            for (int playerId = 1; playerId <= 8; playerId++)
            {
                if ((bits & (1 << (playerId - 1))) == 0) continue;
                uint slot = 0;
                if (GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(playerId, out GamePlayerResources* player))
                    slot = player->r_AILordMinusOne;
                Shared.DebugLogHelper.LogInfo(log,
                    $"Prebuilt player: id={playerId}, nativeLordSlot={slot}, rosterLord={GamePlayerManagerAPI.Instance.GetAILord(playerId)}.");
            }
        }

        private void LogObservedWorkshops(string phase)
        {
            foreach (SpawnObservation item in observed.Values.OrderBy(x => x.Id))
                LogWorkshop(item, phase);
        }

        private void LogWorkshop(SpawnObservation item, string phase)
        {
            if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(item.Id, out GameBuilding* building))
            {
                Shared.DebugLogHelper.LogWarning(log,
                    $"Workshop snapshot unavailable: phase={phase}, player={item.PlayerId}, id={item.Id}, reason=invalid-id.");
                return;
            }
            Shared.DebugLogHelper.LogInfo(log,
                $"Workshop snapshot: phase={phase}, player={item.PlayerId}, id={item.Id}, expectedType={item.Type}, " +
                $"actualOwner={building->r_PlayerIdOwner}, actualType={building->r_BuildingType}, alive={building->r_AliveState}, " +
                $"next={building->r_NextProducedGoodId}, produced={building->r_ProducedGoodId}, aicValue={ReadAicSetting(item.PlayerId, item.Type)}.");
        }

        private static int? ReadAicSetting(int playerId, eStructs type)
        {
            AILords lord = GamePlayerManagerAPI.Instance.GetAILord(playerId);
            var aics = GameAIManagerAPI.Instance.GetAICArray();
            int index = (int)lord;
            return index > 0 && index < aics.Length ? GetAicSetting(aics[index], type) : (int?)null;
        }

        private int ReadPrebuiltBits() => Marshal.ReadInt32(prebuiltPlayersBits);

        private static bool IsWorkshop(eStructs type) =>
            type == eStructs.STRUCT_FLETCHERS_WORKSHOP ||
            type == eStructs.STRUCT_BLACKSMITHS_WORKSHOP ||
            type == eStructs.STRUCT_POLETURNERS_WORKSHOP;

        private static int GetAicSetting(InternalAIC aic, eStructs type)
        {
            if (type == eStructs.STRUCT_FLETCHERS_WORKSHOP) return aic.fletchers_make;
            if (type == eStructs.STRUCT_BLACKSMITHS_WORKSHOP) return aic.blacksmiths_make;
            return aic.poleturners_make;
        }

        private static Tuple<eGoods, eGoods> GetGoods(eStructs type)
        {
            if (type == eStructs.STRUCT_FLETCHERS_WORKSHOP)
                return Tuple.Create(eGoods.STORED_BOWS, eGoods.STORED_CROSSBOWS);
            if (type == eStructs.STRUCT_BLACKSMITHS_WORKSHOP)
                return Tuple.Create(eGoods.STORED_MACES, eGoods.STORED_SWORDS);
            return Tuple.Create(eGoods.STORED_SPEARS, eGoods.STORED_PIKES);
        }

        private static eGoods ChooseVanillaMinority(eStructs type, eGoods first, eGoods second, int firstCount, int secondCount)
        {
            if (type == eStructs.STRUCT_BLACKSMITHS_WORKSHOP)
                return secondCount <= firstCount ? second : first;
            return secondCount < firstCount ? second : first;
        }

        private static void CountAsVanilla(eStructs type, eGoods current, eGoods first,
            ref int firstCount, ref int secondCount)
        {
            if (type == eStructs.STRUCT_BLACKSMITHS_WORKSHOP)
            {
                if (current == eGoods.STORED_SWORDS) secondCount++;
                else firstCount++;
            }
            else if (current == first) firstCount++;
            else secondCount++;
        }

        private static void ValidateInteropLayout()
        {
            if (Marshal.SizeOf(typeof(GameBuilding)) != 0x32C ||
                (int)Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_AliveState)) != 0xD0 ||
                (int)Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_BuildingType)) != 0xD2 ||
                (int)Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_PlayerIdOwner)) != 0xD6 ||
                (int)Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_NextProducedGoodId)) != 0x28E ||
                (int)Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_ProducedGoodId)) != 0x290 ||
                Marshal.SizeOf(typeof(InternalAIC)) != 0x5E4 ||
                (int)Marshal.OffsetOf(typeof(InternalAIC), nameof(InternalAIC.blacksmiths_make)) != 0xCC ||
                (int)Marshal.OffsetOf(typeof(InternalAIC), nameof(InternalAIC.fletchers_make)) != 0xD0 ||
                (int)Marshal.OffsetOf(typeof(InternalAIC), nameof(InternalAIC.poleturners_make)) != 0xD4)
                throw new InvalidOperationException("Installed interop layout differs from audited native workshop layout.");
        }

        private void Fail(string message, Exception exception)
        {
            attempted = true;
            captureNativeStart = false;
            Shared.DebugLogHelper.LogError(log, message + ": " + exception);
        }

        private sealed class SpawnObservation
        {
            internal SpawnObservation(int id, int playerId, eStructs type, uint lordSlotAtSpawn, int? aicAtSpawn)
            { Id = id; PlayerId = playerId; Type = type; LordSlotAtSpawn = lordSlotAtSpawn; AicAtSpawn = aicAtSpawn; }
            internal int Id { get; }
            internal int PlayerId { get; }
            internal eStructs Type { get; }
            internal uint LordSlotAtSpawn { get; }
            internal int? AicAtSpawn { get; }
        }
    }
}
