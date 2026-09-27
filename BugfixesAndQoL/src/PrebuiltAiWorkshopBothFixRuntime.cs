using APIShared;
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using AILords = Enums.AILords;

namespace BugfixesAndQoL
{
    internal sealed unsafe class PrebuiltAiWorkshopBothFixRuntime
    {
        private const int PrebuiltReferenceRva = 0x95FF8;
        private const int Both = -999;
        private const string PrebuiltReferencePattern =
            "8B 0D ?? ?? ?? ?? 8D 46 FF 0F AB C1 41 B8 64 00 00 00 " +
            "89 0D ?? ?? ?? ?? 8B D6 49 8B CD E8 ?? ?? ?? ??";

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly IntPtr prebuiltPlayersBits;
        private readonly Dictionary<int, SpawnObservation> observed = new Dictionary<int, SpawnObservation>();
        private readonly List<Correction> corrected = new List<Correction>();
        private List<IDisposable> subscriptions;
        private bool captureNativeStart;
        private bool reportOnFirstTick;
        private long sessionId;

        internal PrebuiltAiWorkshopBothFixRuntime(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings,
            CrusaderLibraryLoadContext context,
            bool fixedLayoutHashValidated)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (!fixedLayoutHashValidated)
                throw new InvalidOperationException("The audited native workshop layout is unavailable.");

            ReadOnlySpan<byte> memory = context.Memory;
            if (!Shared.NativePatternResolver.MatchesPatternAt(memory, PrebuiltReferenceRva, PrebuiltReferencePattern))
                throw new InvalidOperationException("The prebuilt-player bitfield reference differs from the audited native build.");
            int targetRva = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory, PrebuiltReferenceRva + 2, PrebuiltReferenceRva + 6);
            if (targetRva < 0 || targetRva > memory.Length - sizeof(int))
                throw new InvalidOperationException("The prebuilt-player bitfield target is outside the native image.");
            prebuiltPlayersBits = IntPtr.Add(context.ModuleHandle, targetRva);
            ValidateInteropLayout();
        }

        internal void Install()
        {
            if (subscriptions != null) return;
            var candidates = new List<IDisposable>();
            bool tickSubscribed = false;
            try
            {
                candidates.Add(Shared.MissionEvents.NativeStart.Subscribe(OnNativeStart));
                candidates.Add(BuildingR3EventHooks.OnBuildingSpawn.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(OnBuildingSpawn));
                candidates.Add(Shared.GameplaySessionLifecycle.SubscribeStarted(log, OnSessionStarted));
                candidates.Add(Shared.MissionEvents.Ended.Subscribe(_ => ResetMapState()));
                GameTimeManagerAPI.Instance.OnTick += OnTick;
                tickSubscribed = true;
                subscriptions = candidates;
            }
            catch
            {
                // Only an unpublished initialization candidate may release registrations.
                if (tickSubscribed) GameTimeManagerAPI.Instance.OnTick -= OnTick;
                foreach (IDisposable candidate in candidates) candidate.Dispose();
                throw;
            }
        }

        private void OnNativeStart(MissionLifecycleNotification notification)
        {
            try
            {
                if (notification.IsBeforeInitialization)
                {
                    ResetMapState();
                    captureNativeStart = !notification.Context.IsSave && !notification.Context.IsEditor;
                }
                else
                    captureNativeStart = false;
            }
            catch (Exception ex) { Fail("Native-start capture failed", ex); }
        }

        private void OnBuildingSpawn(BuildingSpawnEventArgs args)
        {
            if (!captureNativeStart || !IsWorkshop(args.Building) || args.PlayerId < 1 || args.PlayerId > 8)
                return;
            try
            {
                int buildingId = checked((int)args.ReturnValue);
                if (buildingId <= 0 || (ReadPrebuiltBits() & (1 << (args.PlayerId - 1))) == 0 ||
                    !GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(
                        args.PlayerId, out GamePlayerResources* player) ||
                    player->r_AILordMinusOne == 0 || ReadAicSetting(args.PlayerId, args.Building) != Both)
                    return;
                observed[buildingId] = new SpawnObservation(buildingId, args.PlayerId, args.Building);
            }
            catch (Exception ex) { Fail("Workshop spawn capture failed", ex); }
        }

        private void OnSessionStarted(Shared.GameplaySessionStartedContext session)
        {
            captureNativeStart = false;
            if (session.Kind != Shared.GameplaySessionStartKind.NewMap ||
                session.IsLoadedSave || session.IsEditor || session.IsReplay || observed.Count == 0)
                return;

            sessionId = session.SessionId;
            bool enabledAtStart = settings.EnableMod && settings.EnablePrebuiltAiWorkshopBothFix;
            reportOnFirstTick = enabledAtStart;
            if (!enabledAtStart) return;
            try { CorrectObservedWorkshops(); }
            catch (Exception ex) { Fail("Workshop correction failed", ex); }
        }

        private void CorrectObservedWorkshops()
        {
            var groups = observed.Values.GroupBy(item => new { item.PlayerId, item.Type })
                .OrderBy(group => group.Key.PlayerId).ThenBy(group => (int)group.Key.Type);
            foreach (var group in groups)
                CorrectGroup(group.Key.PlayerId, group.Key.Type, group.OrderBy(item => item.Id).ToArray());
        }

        private void CorrectGroup(int playerId, eStructs type, SpawnObservation[] items)
        {
            if ((ReadPrebuiltBits() & (1 << (playerId - 1))) == 0 ||
                !GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(
                    playerId, out GamePlayerResources* player) ||
                player->r_AILordMinusOne == 0 || ReadAicSetting(playerId, type) != Both)
            {
                WarnRejectedGroup(playerId, type, "prebuilt marker, Lord, or Both setting changed");
                return;
            }

            var pair = GetGoods(type);
            eGoods vanillaDefault = type == eStructs.STRUCT_BLACKSMITHS_WORKSHOP ? pair.Item2 : pair.Item1;
            var ids = new HashSet<int>(items.Select(item => item.Id));
            var choices = new List<Correction>(items.Length);
            var buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            int firstCount = 0, secondCount = 0;
            for (int spanIndex = 0; spanIndex < buildings.Length; spanIndex++)
            {
                ref GameBuilding other = ref buildings[spanIndex];
                if (other.r_AliveState != AliveState.IsAlive || other.r_PlayerIdOwner != playerId ||
                    other.r_BuildingType != type || ids.Contains(spanIndex + 1))
                    continue;
                CountAsVanilla(type, other.r_NextProducedGoodId, pair.Item1, ref firstCount, ref secondCount);
            }

            // Plan the entire group before writing; a changed building must not receive a partial fix.
            foreach (SpawnObservation item in items)
            {
                if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(item.Id, out GameBuilding* building) ||
                    building->r_AliveState != AliveState.NeedsInit || building->r_PlayerIdOwner != playerId ||
                    building->r_BuildingType != type || building->r_NextProducedGoodId != vanillaDefault ||
                    building->r_ProducedGoodId != vanillaDefault)
                {
                    WarnRejectedGroup(playerId, type, $"building {item.Id} no longer has the audited startup state");
                    return;
                }
                eGoods desired = ChooseVanillaMinority(type, pair.Item1, pair.Item2, firstCount, secondCount);
                choices.Add(new Correction(item.Id, playerId, type, desired));
                CountAsVanilla(type, desired, pair.Item1, ref firstCount, ref secondCount);
            }

            // Recheck every target and retain its validated pointer before the first write.
            // A rejected target leaves the whole group untouched.
            var targets = new GameBuilding*[choices.Count];
            for (int index = 0; index < choices.Count; index++)
            {
                Correction choice = choices[index];
                if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(choice.Id, out GameBuilding* building) ||
                    building->r_AliveState != AliveState.NeedsInit || building->r_PlayerIdOwner != playerId ||
                    building->r_BuildingType != type || building->r_NextProducedGoodId != vanillaDefault ||
                    building->r_ProducedGoodId != vanillaDefault)
                {
                    WarnRejectedGroup(playerId, type, $"building {choice.Id} changed before writing");
                    return;
                }
                targets[index] = building;
            }

            for (int index = 0; index < choices.Count; index++)
            {
                Correction choice = choices[index];
                if (choice.Good == vanillaDefault) continue;
                GameBuilding* building = targets[index];
                building->r_NextProducedGoodId = choice.Good;
                building->r_ProducedGoodId = choice.Good;
                corrected.Add(choice);
            }
        }

        private void OnTick(int tick)
        {
            if (!reportOnFirstTick) return;
            reportOnFirstTick = false;
            if (corrected.Count == 0) return;
            try
            {
                int verified = 0;
                int bows = 0, crossbows = 0, maces = 0, swords = 0, spears = 0, pikes = 0;
                int invalid = 0;
                var workshopGoods = new List<string>(observed.Count);
                foreach (SpawnObservation item in observed.Values.OrderBy(item => item.PlayerId)
                    .ThenBy(item => (int)item.Type).ThenBy(item => item.Id))
                {
                    if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(item.Id, out GameBuilding* building) ||
                        building->r_PlayerIdOwner != item.PlayerId || building->r_BuildingType != item.Type ||
                        building->r_NextProducedGoodId != building->r_ProducedGoodId)
                    {
                        invalid++;
                        workshopGoods.Add($"{item.PlayerId}/{item.Id}=invalid");
                        continue;
                    }
                    eGoods good = building->r_NextProducedGoodId;
                    workshopGoods.Add($"{item.PlayerId}/{item.Id}={good}");
                    if (item.Type == eStructs.STRUCT_FLETCHERS_WORKSHOP && good == eGoods.STORED_BOWS) bows++;
                    else if (item.Type == eStructs.STRUCT_FLETCHERS_WORKSHOP && good == eGoods.STORED_CROSSBOWS) crossbows++;
                    else if (item.Type == eStructs.STRUCT_BLACKSMITHS_WORKSHOP && good == eGoods.STORED_MACES) maces++;
                    else if (item.Type == eStructs.STRUCT_BLACKSMITHS_WORKSHOP && good == eGoods.STORED_SWORDS) swords++;
                    else if (item.Type == eStructs.STRUCT_POLETURNERS_WORKSHOP && good == eGoods.STORED_SPEARS) spears++;
                    else if (item.Type == eStructs.STRUCT_POLETURNERS_WORKSHOP && good == eGoods.STORED_PIKES) pikes++;
                    else invalid++;
                }
                foreach (Correction choice in corrected)
                {
                    if (GameBuildingManagerAPI.Instance.TryGetBuildingById(choice.Id, out GameBuilding* building) &&
                        building->r_PlayerIdOwner == choice.PlayerId && building->r_BuildingType == choice.Type &&
                        building->r_NextProducedGoodId == choice.Good && building->r_ProducedGoodId == choice.Good)
                        verified++;
                }
                Shared.DebugLogHelper.LogInfo(log,
                    $"Prebuilt AI workshop Both fix: session={sessionId}, " +
                    $"captured={observed.Count}, corrected={corrected.Count}, verified={verified}, " +
                    $"goods=bows:{bows},crossbows:{crossbows},maces:{maces},swords:{swords}," +
                    $"spears:{spears},pikes:{pikes}, " +
                    $"workshops=[{string.Join(",", workshopGoods)}], invalid={invalid}, firstTick={tick}.");
                if (verified != corrected.Count)
                    Shared.DebugLogHelper.LogWarning(log,
                        $"Prebuilt AI workshop Both fix: {corrected.Count - verified} corrected workshop(s) changed before the first tick.");
                if (invalid != 0)
                    Shared.DebugLogHelper.LogWarning(log,
                        $"Prebuilt AI workshop Both fix: {invalid} captured workshop(s) had unexpected first-tick goods or identity.");
            }
            catch (Exception ex) { Fail("First-tick verification failed", ex); }
        }

        private void WarnRejectedGroup(int playerId, eStructs type, string reason) =>
            Shared.DebugLogHelper.LogWarning(log,
                $"Prebuilt AI workshop Both fix skipped player={playerId}, type={type}: {reason}.");

        private void ResetMapState()
        {
            captureNativeStart = false;
            reportOnFirstTick = false;
            observed.Clear();
            corrected.Clear();
            sessionId = 0;
        }

        private void Fail(string message, Exception ex)
        {
            captureNativeStart = false;
            reportOnFirstTick = false;
            observed.Clear();
            Shared.DebugLogHelper.LogError(log, $"Prebuilt AI workshop Both fix: {message}: {ex}");
        }

        private int ReadPrebuiltBits() => Marshal.ReadInt32(prebuiltPlayersBits);

        private static bool IsWorkshop(eStructs type) =>
            type == eStructs.STRUCT_FLETCHERS_WORKSHOP ||
            type == eStructs.STRUCT_BLACKSMITHS_WORKSHOP ||
            type == eStructs.STRUCT_POLETURNERS_WORKSHOP;

        private static int? ReadAicSetting(int playerId, eStructs type)
        {
            AILords lord = GamePlayerManagerAPI.Instance.GetAILord(playerId);
            var aics = GameAIManagerAPI.Instance.GetAICArray();
            int index = (int)lord;
            if (index <= 0 || index >= aics.Length) return null;
            InternalAIC aic = aics[index];
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

        private static eGoods ChooseVanillaMinority(eStructs type, eGoods first, eGoods second,
            int firstCount, int secondCount)
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
                throw new InvalidOperationException("Installed interop layout differs from the audited workshop layout.");
        }

        private sealed class SpawnObservation
        {
            internal SpawnObservation(int id, int playerId, eStructs type)
            { Id = id; PlayerId = playerId; Type = type; }
            internal int Id { get; }
            internal int PlayerId { get; }
            internal eStructs Type { get; }
        }

        private sealed class Correction
        {
            internal Correction(int id, int playerId, eStructs type, eGoods good)
            { Id = id; PlayerId = playerId; Type = type; Good = good; }
            internal int Id { get; }
            internal int PlayerId { get; }
            internal eStructs Type { get; }
            internal eGoods Good { get; }
        }
    }
}
