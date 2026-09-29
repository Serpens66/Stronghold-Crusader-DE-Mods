using BepInEx.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Backends.NativeX64;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace ExtraFeatures
{
    internal sealed unsafe class HealerTargetsRuntime
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void BuildPlayerUnitListDelegate(IntPtr manager, int playerId);

        private const int CivilianMask = 1;
        private const int SiegeMask = 2;
        private static HealerTargetsRuntime rootedPublishedInstance;

        private readonly ManualLogSource log;
        private readonly DetourHandle<BuildPlayerUnitListDelegate> listBuilder =
            new DetourHandle<BuildPlayerUnitListDelegate>();
        private HookTransaction transaction;
        private GameTimeManagerAPI timeManager;
        private ulong imageBase;
        private int enabledMask;
        private int callbackDisabled;
        private int callbackFailureLogged;
        private int postStartupLogged;

        private HealerTargetsRuntime(ManualLogSource log) =>
            this.log = log ?? throw new ArgumentNullException(nameof(log));

        internal static HealerTargetsRuntime Install(CrusaderLibraryLoadContext context, ManualLogSource log)
        {
            if (rootedPublishedInstance != null)
                return rootedPublishedInstance;

            var candidate = new HealerTargetsRuntime(log);
            candidate.InstallCandidate(context);
            // Never dispose or unpatch a published hook. The tick publisher and this field
            // keep the callback and its detour alive after the BepInEx startup cleanup.
            Volatile.Write(ref rootedPublishedInstance, candidate);
            return candidate;
        }

        internal void SetEnabled(bool civilians, bool siege)
        {
            Volatile.Write(ref enabledMask,
                (civilians ? CivilianMask : 0) | (siege ? SiegeMask : 0));
        }

        private void InstallCandidate(CrusaderLibraryLoadContext context)
        {
            if (context == null || context.ModuleHandle == IntPtr.Zero ||
                context.Region == null || context.Memory.Length == 0)
                throw new InvalidOperationException("Incomplete native library load context.");

            imageBase = unchecked((ulong)context.ModuleHandle.ToInt64());
            HealerNativeContract.VerifyInstalledBinary(imageBase);
            HealerNativeContract.ProbeBackend(
                imageBase + HealerNativeContract.ListBuilderRva,
                (BuildPlayerUnitListDelegate)((_, __) => { }));
            timeManager = GameTimeManagerAPI.Instance ??
                throw new InvalidOperationException("Persistent game-time publisher unavailable.");

            HookTransaction pending = null;
            bool subscribed = false;
            try
            {
                timeManager.OnTick += OnTick;
                subscribed = true;
                pending = new HookTransaction(
                    context.Region,
                    SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                    new HookTransactionOptions
                    {
                        FailureMode = TransactionFailureMode.RollbackAndThrow,
                        OwnsHooks = true
                    });
                pending.AddDetour(listBuilder,
                    HookTarget.FromAddress(imageBase + HealerNativeContract.ListBuilderRva),
                    (BuildPlayerUnitListDelegate)BuildPlayerUnitList);
                CommitResult result = pending.Commit();
                if (!result.IsCompleteSuccess || !listBuilder.Success)
                    throw new InvalidOperationException("Healer list-builder detour installation failed: " + result);
                HealerNativeContract.ValidateInstalledDetour(
                    listBuilder.Hook as NativeDetour<BuildPlayerUnitListDelegate>,
                    imageBase + HealerNativeContract.ListBuilderRva);
                transaction = pending;
                pending = null;
            }
            catch
            {
                pending?.Dispose();
                if (subscribed) timeManager.OnTick -= OnTick;
                throw;
            }
        }

        private void BuildPlayerUnitList(IntPtr manager, int playerId)
        {
            listBuilder.Original(manager, playerId);
            int mask = Volatile.Read(ref enabledMask);
            if (mask == 0 || Volatile.Read(ref callbackDisabled) != 0)
                return;

            try
            {
                AppendTargets(manager, playerId, mask);
            }
            catch (Exception ex)
            {
                Volatile.Write(ref callbackDisabled, 1);
                if (Interlocked.Exchange(ref callbackFailureLogged, 1) == 0)
                {
                    try
                    {
                        log.LogError("HEALER_TARGETS_CALLBACK_FAILED: playerId=" + playerId +
                            ", manager=0x" + manager.ToInt64().ToString("X") +
                            ", exception=" + ex);
                    }
                    catch { /* Logging cannot escape a native callback. */ }
                }
            }
        }

        private void AppendTargets(IntPtr manager, int playerId, int mask)
        {
            if (playerId <= 0 || playerId >= HealerNativeContract.PlayerSlots ||
                unchecked((ulong)manager.ToInt64()) != imageBase + HealerNativeContract.UnitManagerRva)
                return;

            var unitManager = (GameUnitManager*)manager.ToPointer();
            int nextUnitId = unchecked((int)unitManager->r_NextUnitId);
            if (!HealerListBounds.IsUsableNextUnitId(nextUnitId))
                return;

            int* count = (int*)(imageBase + HealerNativeContract.CountRva +
                (ulong)(playerId * HealerNativeContract.PlayerRecordStride));
            int currentCount = *count;
            if (!HealerListBounds.IsUsableListCount(currentCount))
                return;

            short* ids = (short*)(imageBase + HealerNativeContract.IdListRva +
                (ulong)(playerId * HealerNativeContract.IdListStride));
            int* globalIds = (int*)(imageBase + HealerNativeContract.GlobalIdListRva +
                (ulong)(playerId * HealerNativeContract.GlobalIdListStride));
            var seen = new bool[HealerNativeContract.ListCapacity];
            for (int index = 0; index < currentCount; index++)
            {
                int existingId = ids[index];
                if (existingId > 0 && existingId < nextUnitId)
                    seen[existingId] = true;
            }

            // Native game IDs index this array directly; slot zero is LastOrderedUnit.
            GameUnit* units = &unitManager->GameUnitArray;
            for (int unitId = 1; unitId < nextUnitId && currentCount < HealerNativeContract.ListCapacity; unitId++)
            {
                GameUnit* unit = units + unitId;
                bool siege = (mask & SiegeMask) != 0 && IsSiegeEngine(unit->r_UnitChimp);
                bool civilian = (mask & CivilianMask) != 0 && IsHumanCivilian(unit->r_UnitChimp);
                if ((!siege && !civilian) || seen[unitId] ||
                    unit->r_ControllableForPlayerId != playerId ||
                    unit->r_AliveState != AliveState.IsAlive || unit->r_GlobalId == 0 ||
                    unit->r_CurrentHealth == 0 || unit->r_CurrentHealth >= unit->r_MaxHealth)
                    continue;

                byte* record = (byte*)unit;
                if (*(short*)(record + 0x29C) != 0 || *(byte*)(record + 0x458) != 0)
                    continue;

                ids[currentCount] = checked((short)unitId);
                globalIds[currentCount] = unchecked((int)unit->r_GlobalId);
                currentCount++;
                seen[unitId] = true;
            }
            *count = currentCount;
        }

        private static bool IsSiegeEngine(eChimps type)
        {
            switch (type)
            {
                case eChimps.CHIMP_TYPE_CATAPULT:
                case eChimps.CHIMP_TYPE_TREBUCHET:
                case eChimps.CHIMP_TYPE_MANGONEL:
                case eChimps.CHIMP_TYPE_SIEGE_TOWER:
                case eChimps.CHIMP_TYPE_BATTERING_RAM:
                case eChimps.CHIMP_TYPE_PORTABLE_SHIELD:
                case eChimps.CHIMP_TYPE_BALLISTA:
                case eChimps.CHIMP_TYPE_ARAB_BALLISTA:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsHumanCivilian(eChimps type)
        {
            switch (type)
            {
                case eChimps.CHIMP_TYPE_PEASANT:
                case eChimps.CHIMP_TYPE_WOODCUTTER:
                case eChimps.CHIMP_TYPE_FLETCHER:
                case eChimps.CHIMP_TYPE_HUNTER:
                case eChimps.CHIMP_TYPE_QUARRY_MASON:
                case eChimps.CHIMP_TYPE_QUARRY_GRUNT:
                case eChimps.CHIMP_TYPE_PITCHMAN:
                case eChimps.CHIMP_TYPE_FARMER_WHEAT:
                case eChimps.CHIMP_TYPE_FARMER_HOPS:
                case eChimps.CHIMP_TYPE_FARMER_APPLE:
                case eChimps.CHIMP_TYPE_FARMER_CATTLE:
                case eChimps.CHIMP_TYPE_MILLER:
                case eChimps.CHIMP_TYPE_BAKER:
                case eChimps.CHIMP_TYPE_BREWER:
                case eChimps.CHIMP_TYPE_POLETURNER:
                case eChimps.CHIMP_TYPE_BLACKSMITH:
                case eChimps.CHIMP_TYPE_ARMOURER:
                case eChimps.CHIMP_TYPE_TANNER:
                case eChimps.CHIMP_TYPE_PRIEST:
                case eChimps.CHIMP_TYPE_HEALER:
                case eChimps.CHIMP_TYPE_DRUNKARD:
                case eChimps.CHIMP_TYPE_INNKEEPER:
                case eChimps.CHIMP_TYPE_TRADER:
                case eChimps.CHIMP_TYPE_FIREMAN:
                case eChimps.CHIMP_TYPE_LADY:
                case eChimps.CHIMP_TYPE_JESTER:
                case eChimps.CHIMP_TYPE_MOTHER:
                case eChimps.CHIMP_TYPE_CHILD:
                case eChimps.CHIMP_TYPE_JUGGLER:
                case eChimps.CHIMP_TYPE_FIREEATER:
                    return true;
                default:
                    return false;
            }
        }

        private void OnTick(int tick)
        {
            if (Volatile.Read(ref postStartupLogged) != 0)
                return;
            if (Interlocked.Exchange(ref postStartupLogged, 1) == 0)
                log.LogInfo("HEALER_TARGETS_POST_STARTUP: tick=" + tick +
                    ", civilians=" + ((Volatile.Read(ref enabledMask) & CivilianMask) != 0) +
                    ", siege=" + ((Volatile.Read(ref enabledMask) & SiegeMask) != 0));
        }
    }
}
