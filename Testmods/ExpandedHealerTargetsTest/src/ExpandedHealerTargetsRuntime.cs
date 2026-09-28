using BepInEx.Configuration;
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

namespace ExpandedHealerTargetsTest
{
    internal sealed unsafe class ExpandedHealerTargetsRuntime
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void BuildPlayerUnitListDelegate(IntPtr manager, int playerId);

        private readonly ManualLogSource log;
        private readonly bool healSiege;
        private readonly bool healCivilians;
        private readonly DetourHandle<BuildPlayerUnitListDelegate> listBuilder =
            new DetourHandle<BuildPlayerUnitListDelegate>();
        private HookTransaction transaction;
        private GameTimeManagerAPI timeManager;
        private ulong imageBase;
        private int postStartupLogged;
        private int callbackDisabled;
        private int callbackFailures;
        private int diagnosticReports;
        private int addedSiege;
        private int addedCivilians;
        private int rejectedCivilianOwner;
        private int rejectedCivilianState;
        private int rejectedCivilianHealth;
        private int rejectedCivilianNativeFlags;
        private int firstAddedSiegeType;
        private int firstAddedCivilianType;
        private int firstRejectedCivilianOwnerType;
        private int firstRejectedCivilianStateType;
        private int firstRejectedCivilianHealthType;
        private int firstRejectedCivilianNativeFlagsType;

        private ExpandedHealerTargetsRuntime(ManualLogSource log,
            ConfigEntry<bool> healSiege, ConfigEntry<bool> healCivilians)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.healSiege = (healSiege ?? throw new ArgumentNullException(nameof(healSiege))).Value;
            this.healCivilians = (healCivilians ?? throw new ArgumentNullException(nameof(healCivilians))).Value;
        }

        internal static ExpandedHealerTargetsRuntime Install(CrusaderLibraryLoadContext context,
            ManualLogSource log, ConfigEntry<bool> healSiege, ConfigEntry<bool> healCivilians)
        {
            var candidate = new ExpandedHealerTargetsRuntime(log, healSiege, healCivilians);
            candidate.InstallCandidate(context);
            return candidate;
        }

        private void InstallCandidate(CrusaderLibraryLoadContext context)
        {
            if (context.ModuleHandle == IntPtr.Zero || context.Region == null || context.Memory.Length == 0)
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

                // Publication ends rollback eligibility; both handle and callback stay rooted.
                transaction = pending;
                pending = null;
                Info("HEALER_TARGETS_HOOK_READY: rva=0x181340, scheme=Indirect, displaced=10, " +
                     "nativeHash=" + HealerNativeContract.NativeSha256);
            }
            catch
            {
                // Only an unpublished candidate is ever rolled back.
                pending?.Dispose();
                if (subscribed) timeManager.OnTick -= OnTick;
                throw;
            }
        }

        private void BuildPlayerUnitList(IntPtr manager, int playerId)
        {
            listBuilder.Original(manager, playerId);
            if (Volatile.Read(ref callbackDisabled) != 0 ||
                (!healSiege && !healCivilians)) return;

            try
            {
                AppendTargets(manager, playerId);
            }
            catch
            {
                // The installed hook remains in place and calls Vanilla after fail-open.
                Interlocked.Increment(ref callbackFailures);
                Volatile.Write(ref callbackDisabled, 1);
            }
        }

        private void AppendTargets(IntPtr manager, int playerId)
        {
            if (playerId <= 0 || playerId >= HealerNativeContract.PlayerSlots ||
                unchecked((ulong)manager.ToInt64()) != imageBase + HealerNativeContract.UnitManagerRva)
                return;

            var unitManager = (GameUnitManager*)manager.ToPointer();
            int nextUnitId = checked((int)unitManager->r_NextUnitId);
            if (nextUnitId < 1 || nextUnitId > HealerNativeContract.ListCapacity)
                throw new InvalidOperationException("Unit ID upper bound exceeds the audited native list capacity.");

            int* count = (int*)(imageBase + HealerNativeContract.CountRva +
                (ulong)(playerId * HealerNativeContract.PlayerRecordStride));
            int currentCount = *count;
            if (currentCount < 0 || currentCount >= HealerNativeContract.ListCapacity) return;
            short* ids = (short*)(imageBase + HealerNativeContract.IdListRva +
                (ulong)(playerId * HealerNativeContract.IdListStride));
            int* globalIds = (int*)(imageBase + HealerNativeContract.GlobalIdListRva +
                (ulong)(playerId * HealerNativeContract.GlobalIdListStride));
            var seen = new bool[HealerNativeContract.ListCapacity];
            for (int index = 0; index < currentCount; index++)
            {
                int existingId = ids[index];
                if (existingId > 0 && existingId < nextUnitId) seen[existingId] = true;
            }

            // Native game IDs index the manager's array directly: slot zero is LastOrderedUnit.
            GameUnit* units = &unitManager->GameUnitArray;
            for (int unitId = 1; unitId < nextUnitId && currentCount < HealerNativeContract.ListCapacity; unitId++)
            {
                GameUnit* unit = units + unitId;
                bool siege = healSiege && IsSiegeEngine(unit->r_UnitChimp);
                bool civilian = healCivilians && IsHumanCivilian(unit->r_UnitChimp);
                if ((!siege && !civilian) || seen[unitId]) continue;

                if (unit->r_ControllableForPlayerId != playerId || playerId == 0)
                {
                    if (civilian)
                    {
                        Interlocked.Increment(ref rejectedCivilianOwner);
                        CaptureFirstType(ref firstRejectedCivilianOwnerType, unit->r_UnitChimp);
                    }
                    continue;
                }
                if (unit->r_AliveState != AliveState.IsAlive || unit->r_GlobalId == 0)
                {
                    if (civilian)
                    {
                        Interlocked.Increment(ref rejectedCivilianState);
                        CaptureFirstType(ref firstRejectedCivilianStateType, unit->r_UnitChimp);
                    }
                    continue;
                }
                if (unit->r_CurrentHealth == 0 || unit->r_CurrentHealth >= unit->r_MaxHealth)
                {
                    if (civilian)
                    {
                        Interlocked.Increment(ref rejectedCivilianHealth);
                        CaptureFirstType(ref firstRejectedCivilianHealthType, unit->r_UnitChimp);
                    }
                    continue;
                }
                byte* record = (byte*)unit;
                // These two Vanilla selector guards must hold after list insertion as well.
                if (*(short*)(record + 0x29C) != 0 || *(byte*)(record + 0x458) != 0)
                {
                    if (civilian)
                    {
                        Interlocked.Increment(ref rejectedCivilianNativeFlags);
                        CaptureFirstType(ref firstRejectedCivilianNativeFlagsType, unit->r_UnitChimp);
                    }
                    continue;
                }

                ids[currentCount] = checked((short)unitId);
                globalIds[currentCount] = unchecked((int)unit->r_GlobalId);
                currentCount++;
                seen[unitId] = true;
                if (siege)
                {
                    Interlocked.Increment(ref addedSiege);
                    CaptureFirstType(ref firstAddedSiegeType, unit->r_UnitChimp);
                }
                else
                {
                    Interlocked.Increment(ref addedCivilians);
                    CaptureFirstType(ref firstAddedCivilianType, unit->r_UnitChimp);
                }
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

        private static void CaptureFirstType(ref int slot, eChimps type) =>
            Interlocked.CompareExchange(ref slot, (int)type + 1, 0);

        private static string TypeName(int stored) =>
            stored == 0 ? "none" : ((eChimps)(stored - 1)).ToString();

        private void OnTick(int tick)
        {
            if (Interlocked.Exchange(ref postStartupLogged, 1) == 0)
                Info("HEALER_TARGETS_POST_STARTUP: tick=" + tick +
                     ", siege=" + healSiege + ", civilians=" + healCivilians);
            if (Interlocked.CompareExchange(ref diagnosticReports, 0, 0) >= 5 || tick % 256 != 0)
                return;
            Interlocked.Increment(ref diagnosticReports);
            Info("HEALER_TARGETS_DIAGNOSTIC: addedSiege=" + Volatile.Read(ref addedSiege) +
                 "(" + TypeName(Volatile.Read(ref firstAddedSiegeType)) + ")" +
                 ", addedCivilians=" + Volatile.Read(ref addedCivilians) +
                 "(" + TypeName(Volatile.Read(ref firstAddedCivilianType)) + ")" +
                 ", civilianOwner=" + Volatile.Read(ref rejectedCivilianOwner) +
                 "(" + TypeName(Volatile.Read(ref firstRejectedCivilianOwnerType)) + ")" +
                 ", civilianState=" + Volatile.Read(ref rejectedCivilianState) +
                 "(" + TypeName(Volatile.Read(ref firstRejectedCivilianStateType)) + ")" +
                 ", civilianHealth=" + Volatile.Read(ref rejectedCivilianHealth) +
                 "(" + TypeName(Volatile.Read(ref firstRejectedCivilianHealthType)) + ")" +
                 ", civilianNativeFlags=" + Volatile.Read(ref rejectedCivilianNativeFlags) +
                 "(" + TypeName(Volatile.Read(ref firstRejectedCivilianNativeFlagsType)) + ")" +
                 ", callbackFailures=" + Volatile.Read(ref callbackFailures));
        }

        private void Info(string message) =>
            log.LogInfo($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");
    }
}
