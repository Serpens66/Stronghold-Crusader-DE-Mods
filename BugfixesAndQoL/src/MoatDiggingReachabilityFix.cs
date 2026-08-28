// Feature: Let valid moat-digging orders traverse already completed friendly moats.
using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Runtime.InteropServices;
using Zhuqiaomon.Assembly;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;
using Zhuqiaomon.Memory;

namespace BugfixesAndQoL
{
    internal sealed unsafe class MoatDiggingReachabilityFix : IDisposable
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int FindNearestFriendlyMoatDelegate(
            IntPtr tileManager,
            int playerId,
            int unitId,
            int relationshipMode);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int GetMoatIdAtTileDelegate(IntPtr tileManager, int tileId);

        private const int DigMoatModePatternRva = 0x8D3C2;
        private const int CursorReachabilityPatternRva = 0x8F3A8;
        private const int GetMoatIdAtTilePatternRva = 0x69560;
        private const int FindNearestFriendlyMoatPatternRva = 0x69D60;
        private const int CursorReachabilityHookOffset = 29;
        private const int CursorReachabilityHookLength = 12;
        private const int MoatRecordArrayOffset = 0x1F3EE30;
        private const int MoatRecordCountOffset = 0x2038E30;
        private const int MoatRecordSize = 0x10;
        private const int MoatOwnerOffset = 0x0C;

        private const string DigMoatModePattern =
            "44 39 25 ?? ?? ?? ?? 74 3C 48 8B CE E8 ?? ?? ?? ?? " +
            "85 C0 74 30 B8 01 00 00 00 44 8B E8 89 44 24 54";

        private const string CursorReachabilityPattern =
            "44 8B 0D ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? " +
            "44 8B 05 ?? ?? ?? ?? 41 8B D6 E8 ?? ?? ?? ?? " +
            "85 C0 74 11 44 8B BC 24 C0 00 00 00";

        private const string GetMoatIdAtTilePattern =
            "48 63 C2 0F B7 84 41 ?? ?? ?? ?? C3 CC CC CC";

        private const string FindNearestFriendlyMoatPattern =
            "44 89 44 24 18 89 54 24 10 55 56 57 41 54 41 55 41 56 " +
            "48 83 EC 68 48 8B E9 48 8D 3D ?? ?? ?? ?? 45 8B F1 " +
            "48 8D 87 1C 07 00 00 4D 63 C8 45 33 E4";

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly HookTransaction transaction;
        private readonly int* digMoatMode;
        private readonly int* targetTileX;
        private readonly int* targetTileY;
        private HookRef<X64InlineHook> cursorReachabilityHook = new HookRef<X64InlineHook>();
        private GetMoatIdAtTileDelegate getMoatIdAtTile;
        private FindNearestFriendlyMoatDelegate originalFindNearestFriendlyMoat;
        private FindNearestFriendlyMoatDelegate rootedFindNearestFriendlyMoat;
        private NativeDetour findNearestFriendlyMoatDetour;
        private bool firstDirectedTargetLogged;
        private bool directedTargetFailureLogged;
        private bool cursorFailureLogged;
        private bool disposed;

        public MoatDiggingReachabilityFix(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

            // The moat record array is not exposed by the Script Extender. Its fixed
            // offsets below are validated for the canonical DLL and must fail closed
            // instead of being guessed for a later game version.
            if (!referenceHashMatches)
            {
                throw new InvalidOperationException(
                    "The moat-digging reachability fix requires the validated CrusaderDE.dll layout.");
            }

            Shared.NativeResolution modeResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                DigMoatModePattern,
                DigMoatModePatternRva,
                referenceHashMatches,
                "DigMoat cursor mode",
                log: null);
            Shared.NativeResolution cursorResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                CursorReachabilityPattern,
                CursorReachabilityPatternRva,
                referenceHashMatches,
                "DigMoat cursor reachability check",
                log: null);
            Shared.NativeResolution moatLookupResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                GetMoatIdAtTilePattern,
                GetMoatIdAtTilePatternRva,
                referenceHashMatches,
                "moat ID lookup by tile",
                log: null);
            Shared.NativeResolution moatSearchResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                FindNearestFriendlyMoatPattern,
                FindNearestFriendlyMoatPatternRva,
                referenceHashMatches,
                "nearest friendly moat search",
                log: null);

            int modeRva = ResolveGlobalRva(
                memory,
                modeResolution.Rva + 3,
                modeResolution.Rva + 7,
                "DigMoat cursor mode");
            int targetYRva = ResolveGlobalRva(
                memory,
                cursorResolution.Rva + 3,
                cursorResolution.Rva + 7,
                "cursor target Y");
            int targetXRva = ResolveGlobalRva(
                memory,
                cursorResolution.Rva + 17,
                cursorResolution.Rva + 21,
                "cursor target X");
            int hookRva = checked(cursorResolution.Rva + CursorReachabilityHookOffset);
            ValidateHookSpan(memory, hookRva);

            digMoatMode = (int*)(libraryBase + unchecked((ulong)modeRva));
            targetTileX = (int*)(libraryBase + unchecked((ulong)targetXRva));
            targetTileY = (int*)(libraryBase + unchecked((ulong)targetYRva));

            try
            {
                transaction = new HookTransaction(
                    memory,
                    libraryBase,
                    loggerFactory: null,
                    failureMode: TransactionFailureMode.RollbackAndThrow);
                transaction.AddContextHook(
                    ref cursorReachabilityHook,
                    libraryBase + unchecked((ulong)hookRva),
                    AllowFriendlyPlannedMoatCursor,
                    regs: X64SmartCPUContextRegs.All,
                    hookSize: CursorReachabilityHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.Commit();

                if (!cursorReachabilityHook.Success)
                    throw new InvalidOperationException("The DigMoat cursor reachability hook was not installed.");

                getMoatIdAtTile = Marshal.GetDelegateForFunctionPointer<GetMoatIdAtTileDelegate>(
                    (IntPtr)(libraryBase + unchecked((ulong)moatLookupResolution.Rva)));
                rootedFindNearestFriendlyMoat = DirectCommandedMoatTarget;
                IntPtr moatSearchAddress = (IntPtr)(libraryBase + unchecked((ulong)moatSearchResolution.Rva));
                findNearestFriendlyMoatDetour = new NativeDetour(
                    moatSearchAddress,
                    Marshal.GetFunctionPointerForDelegate(rootedFindNearestFriendlyMoat),
                    new NativeDetourConfig { ManualApply = true });
                originalFindNearestFriendlyMoat =
                    findNearestFriendlyMoatDetour.GenerateTrampoline<FindNearestFriendlyMoatDelegate>();
                findNearestFriendlyMoatDetour.Apply();

                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"Bugfixes and QoL moat-digging reachability fix installed: " +
                    $"modeMethod={modeResolution.Method}, cursorMethod={cursorResolution.Method}, " +
                    $"lookupMethod={moatLookupResolution.Method}, searchMethod={moatSearchResolution.Method}, " +
                    $"modeRva=0x{modeRva:X}, targetXRva=0x{targetXRva:X}, " +
                    $"targetYRva=0x{targetYRva:X}, hookRva=0x{hookRva:X}, " +
                    $"lookupRva=0x{moatLookupResolution.Rva:X}, searchRva=0x{moatSearchResolution.Rva:X}.");
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            findNearestFriendlyMoatDetour?.Dispose();
            findNearestFriendlyMoatDetour = null;
            originalFindNearestFriendlyMoat = null;
            rootedFindNearestFriendlyMoat = null;
            getMoatIdAtTile = null;
            transaction?.Unload();
            transaction?.Dispose();
        }

        private int DirectCommandedMoatTarget(
            IntPtr tileManager,
            int playerId,
            int unitId,
            int relationshipMode)
        {
            try
            {
                if (IsEnabled && relationshipMode == 1 &&
                    GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) &&
                    unit != null &&
                    unit->r_AI_LastIssuedTribeCommand == (ushort)TribeAICommand.DigMoatTileId &&
                    TryGetFriendlyPlannedMoatId(
                        tileManager,
                        playerId,
                        unit->r_ContextTargetTileX,
                        unit->r_ContextTargetTileY,
                        out int commandedMoatId))
                {
                    if (!firstDirectedTargetLogged)
                    {
                        firstDirectedTargetLogged = true;
                        Shared.DebugLogHelper.LogDebug(
                            log,
                            $"Bugfixes and QoL first directed moat target selected: unit={unitId}, " +
                            $"player={playerId}, moat={commandedMoatId}, " +
                            $"target=({unit->r_ContextTargetTileX},{unit->r_ContextTargetTileY}).");
                    }

                    // Vanilla sets its moat-capable path mode immediately after this lookup.
                    // Returning the commanded record preserves that pathing while avoiding the
                    // unrelated nearest-reachable moat which otherwise replaces the order.
                    return commandedMoatId;
                }
            }
            catch (Exception ex)
            {
                if (!directedTargetFailureLogged)
                {
                    directedTargetFailureLogged = true;
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Bugfixes and QoL directed moat selection failed once; Vanilla selection remains active: {ex}");
                }
            }

            return originalFindNearestFriendlyMoat(tileManager, playerId, unitId, relationshipMode);
        }

        private void AllowFriendlyPlannedMoatCursor(NativePointer<X64SmartCPUContext> context)
        {
            X64SmartCPUContext* registers = context.Pointer;
            if (unchecked((uint)registers->RAX) != 0 || !IsEnabled || *digMoatMode == 0)
                return;

            try
            {
                int playerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
                if (TryGetFriendlyPlannedMoatId(
                    GameTileManagerPointer,
                    playerId,
                    *targetTileX,
                    *targetTileY,
                    out _))
                    registers->RAX = 1;
            }
            catch (Exception ex)
            {
                if (cursorFailureLogged)
                    return;

                cursorFailureLogged = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL moat cursor validation failed once; Vanilla cursor behavior remains active: {ex}");
            }
        }

        private bool IsEnabled =>
            !disposed && settings.EnableMod && settings.EnableMoatDiggingReachabilityFix;

        private IntPtr GameTileManagerPointer =>
            (IntPtr)GameGlobalsManager.Instance.GameTileManagerVA;

        private bool TryGetFriendlyPlannedMoatId(
            IntPtr tileManager,
            int playerId,
            int tileX,
            int tileY,
            out int moatId)
        {
            moatId = 0;
            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            if (tileManager == IntPtr.Zero || !playerApi.IsPlayerIdValid(playerId))
                return false;

            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            if (!tileApi.IsTileInsideMapBounds(tileX, tileY))
                return false;

            int tileId = tileApi.GetTileId(tileX, tileY);
            if (!tileApi.IsValidTileId(tileId) ||
                !tileApi.HasTilePropertyFlag(tileId, TilePropertyFlag.PlannedMoat))
            {
                return false;
            }

            moatId = getMoatIdAtTile(tileManager, tileId);
            int moatCount = *(int*)((byte*)tileManager.ToPointer() + MoatRecordCountOffset);
            if (moatId <= 0 || moatId >= moatCount)
            {
                moatId = 0;
                return false;
            }

            byte* moatRecord = (byte*)tileManager.ToPointer() +
                MoatRecordArrayOffset + moatId * MoatRecordSize;
            int moatOwnerId = moatRecord[MoatOwnerOffset];
            if (!playerApi.IsPlayerIdValid(moatOwnerId))
            {
                moatId = 0;
                return false;
            }

            return moatOwnerId == playerId || playerApi.IsPlayerAlliedTo(playerId, moatOwnerId);
        }

        private static int ResolveGlobalRva(
            ReadOnlySpan<byte> memory,
            int displacementRva,
            int nextInstructionRva,
            string label)
        {
            int resolvedRva = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                displacementRva,
                nextInstructionRva);
            if (resolvedRva < 0 || resolvedRva > memory.Length - sizeof(int))
                throw new InvalidOperationException($"The native {label} global is outside CrusaderDE.dll.");
            return resolvedRva;
        }

        private static void ValidateHookSpan(ReadOnlySpan<byte> memory, int hookRva)
        {
            byte[] expected =
            {
                0x85, 0xC0, 0x74, 0x11,
                0x44, 0x8B, 0xBC, 0x24, 0xC0, 0x00, 0x00, 0x00
            };
            if (hookRva < 0 || hookRva > memory.Length - expected.Length ||
                !memory.Slice(hookRva, expected.Length).SequenceEqual(expected))
            {
                throw new InvalidOperationException("The native DigMoat cursor hook span did not match the validated instructions.");
            }
        }
    }
}
