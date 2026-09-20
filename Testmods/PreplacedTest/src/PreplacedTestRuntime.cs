using BepInEx.Logging;
using R3;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Context;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace PreplacedTest
{
    internal sealed unsafe class PreplacedTestRuntime
    {
        private const int MaxPlayablePlayerId = 8;
        private const int PlayerRuntimeStateStride = 0x583C;
        private const int CrushedCounterRelativeOffset = 0x7E4;
        private const int WoodSearchCooldownRelativeOffset = 0x167C;
        private const int ConstructBuildingErrorRva = 0x60AD4AC;
        private const int FarmPlacementOffsetTableRva = 0x2D13B0;
        private const int NativePathManagerRva = 0x60AD660;
        private const int NativePclGridRva = 0x50EC690;
        private const int NativePclGridEndRva = 0x51890D0;
        private const int NativePclEntrySize = sizeof(ushort);
        private const int NativePclEntryCount = (NativePclGridEndRva - NativePclGridRva) / NativePclEntrySize;
        private const int LegacyPlayerStateCopyRva = 0xD4290;
        private const int LegacyPlayerStateCopyCallSiteRva = 0x96CE;
        private const int LegacyPlayerStateSourceRva = 0x37CC7EC;
        private const int CurrentPlayerStateDestinationRva = 0x379ADD0;
        private const int ActivePlayerRuntimeStateBaseRva = 0x379D0CC;
        // These are the exact per-player coordinates used as the BFS origin by
        // RVAs 0x575B0, 0x57B80 and 0x58020. They are not the AIV keep-door fields.
        private const int NativeEconomyStartXRva = 0x379AFA8;
        private const int NativeEconomyStartYRva = 0x379AFAC;
        private const int PlayerResourcesOffsetInSerializedRecord =
            ActivePlayerRuntimeStateBaseRva - CurrentPlayerStateDestinationRva;
        private const int SerializedCrushedCounterOffset =
            PlayerResourcesOffsetInSerializedRecord + CrushedCounterRelativeOffset;
        private const int MapFormatVersionRva = 0x32DC084;
        private const int LegacyPlayerStateCopyVersionExclusive = 0xD5;
        private const int LegacyPlayerStateStride = 0x39F4;
        private const int SerializedPlayerRecordCount = 9;
        private const int MaximumPortalRecordCount = 200;
        private const int PortalRecordStrideDwords = 0x81;
        private const int PortalThirdPclOffsetDwords = 0x883;
        private const int EconomyGridWidth = 160;
        private const int NativeTileGridWidth = 800;
        private const int EconomyGridCellCount = EconomyGridWidth * EconomyGridWidth;
        private const int EconomyGridCellStride = 0x30;
        private const int EconomyGridBaseOffset = 0x5B830;
        private const int PlacementReachabilityRouteCallSiteRva = 0xC3C5D;
        private const int FarmPlacementOffsetTablePairCount = 32;
        private const int EconomyCoarseCellTileSize = 5;

        private const string AllocateSpecPattern =
            "48 89 74 24 10 57 48 83 EC 20 BF 01 00 00 00 48 8D 81 9C 6D 00 00";
        private const string ActiveLayoutReferencePattern =
            "48 63 F2 48 8D 05 ?? ?? ?? ?? 4C 69 CE 3C 58 00 00";
        private const string EconomyOxenPattern =
            "48 89 5C 24 20 56 57 41 54 48 83 EC 40 48 63 FA";
        private const string EconomyQuarryPattern =
            "40 53 55 56 48 83 EC 50 8B F2 48 8B D9 44 8B C2";
        private const string EconomyWoodPattern =
            "48 89 5C 24 08 48 89 74 24 10 57 48 83 EC 40 8B FA 48 8B D9 44 8B C2 48 8D 0D ?? ?? ?? ?? BE 33 00 00 00";
        private const string FarmSearchPattern =
            "44 89 44 24 18 89 54 24 10 53 41 57 48 81 EC A8 00 00 00";
        private const string ResourceSearchPattern =
            "41 54 41 55 48 83 EC 18 45 33 ED 48 C7 81 30 78 18 00 01 00 00 00";
        private const string WoodSearchPattern =
            "40 53 55 41 56 41 57 48 83 EC 18 33 C0 48 C7 81 30 78 18 00 01 00 00 00";
        private const string NearbySearchPattern =
            "41 56 48 83 EC 10 48 C7 81 30 78 18 00 01 00 00 00 45 33 F6";
        private const string EconomyGridUpdatePattern =
            "40 53 56 48 83 EC 38 83 3D ?? ?? ?? ?? 00 8B F2 48 8B D9 0F 84";
        private const string InitializeEconomyAvailabilityPattern =
            "48 89 5C 24 10 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 10 4C 8B D1 48 63 C2 4C 69 C0 3C 58 00 00";
        private const string LegacyPlayerStateCopyPattern =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 48 89 7C 24 20 41 54 41 56 41 57 48 83 EC 20 48 8D 2D ?? ?? ?? ?? BB 60 0D 03 00";

        private const int AllocateSpecRva = 0x50680;
        private const int ActiveLayoutReferenceRva = 0x55F64;
        private const int EconomyOxenRva = 0x50F90;
        private const int EconomyQuarryRva = 0x51270;
        private const int EconomyWoodRva = 0x51540;
        private const int FarmSearchRva = 0x575B0;
        private const int ResourceSearchRva = 0x57B80;
        private const int WoodSearchRva = 0x58020;
        private const int WoodScoreFloorHookRva = 0x58057;
        private const int WoodScoreFloorHookLength = 15;
        // Audited separately: this AIV open-area search also reads byte+04, but it
        // is not part of the external economy census/search pipeline fixed here.
        private const int AlternativeOpenAreaSearchRva = 0x583A0;
        // Called only by 0x54CC0 and performs its own E2610 check for AIV placement.
        // It is deliberately outside the player-specific external-economy overlay.
        private const int AivReachableOpenAreaSearchRva = 0x58BE0;
        private const int NearbySearchRva = 0x58950;
        private const int RegionPairReachabilityRva = 0xE2610;
        private const int EconomyGridUpdateRva = 0x50720;
        private const int InitializeEconomyAvailabilityRva = 0x55FE0;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int AllocateSpecDelegate(ulong state, int playerId);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void EconomyPlayerDelegate(ulong state, int playerId);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate long FarmSearchDelegate(ulong state, int playerId, int desiredStructureType);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void ResourceSearchDelegate(ulong state, int playerId, int mode);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void WoodSearchDelegate(ulong state, int playerId);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void NearbySearchDelegate(ulong state, uint coarseX, uint coarseY);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void EconomyGridUpdateDelegate(ulong state, int mode);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void InitializeEconomyAvailabilityDelegate(ulong state, int playerId);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void LegacyPlayerStateCopyDelegate();

        private readonly ManualLogSource log;
        private readonly LegacyRuinTimerFix legacyRuinTimerFix = new LegacyRuinTimerFix();
        private readonly PreplacedEconomyAccessFix economyAccessFix = new PreplacedEconomyAccessFix();
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly Dictionary<int, PlayerSession> players = new Dictionary<int, PlayerSession>();
        private readonly Stack<DamageContext> pendingDamage = new Stack<DamageContext>();

        private readonly DetourHandle<AllocateSpecDelegate> allocateHook =
            new DetourHandle<AllocateSpecDelegate>();
        private readonly DetourHandle<EconomyPlayerDelegate> economyOxenHook =
            new DetourHandle<EconomyPlayerDelegate>();
        private readonly DetourHandle<EconomyPlayerDelegate> economyQuarryHook =
            new DetourHandle<EconomyPlayerDelegate>();
        private readonly DetourHandle<EconomyPlayerDelegate> economyWoodHook =
            new DetourHandle<EconomyPlayerDelegate>();
        private readonly DetourHandle<FarmSearchDelegate> farmSearchHook =
            new DetourHandle<FarmSearchDelegate>();
        private readonly DetourHandle<ResourceSearchDelegate> resourceSearchHook =
            new DetourHandle<ResourceSearchDelegate>();
        private readonly DetourHandle<WoodSearchDelegate> woodSearchHook =
            new DetourHandle<WoodSearchDelegate>();
        private readonly DetourHandle<NearbySearchDelegate> nearbySearchHook =
            new DetourHandle<NearbySearchDelegate>();
        private readonly DetourHandle<EconomyGridUpdateDelegate> economyGridUpdateHook =
            new DetourHandle<EconomyGridUpdateDelegate>();
        private readonly DetourHandle<LegacyPlayerStateCopyDelegate> legacyPlayerStateCopyHook =
            new DetourHandle<LegacyPlayerStateCopyDelegate>();
        private readonly HookHandle<X64InlineHook> woodScoreFloorHook =
            new HookHandle<X64InlineHook>();

        private InitializeEconomyAvailabilityDelegate initializeEconomyAvailabilityNative;
        private HookTransaction transaction;
        private ulong activeLayoutIndexBase;
        private ulong nativeModuleBase;
        private ushort* nativePclGrid;
        private ulong lastAivState;
        private int mapSequence;

        [ThreadStatic] private static int nearbyEconomyPlayerId;
        [ThreadStatic] private static ulong nearbyEconomyState;
        [ThreadStatic] private static bool resolvingEconomyOverlayRoutes;
        [ThreadStatic] private static bool reconcilingEconomyAvailability;
        [ThreadStatic] private static Stack<EconomyGridOverlayScope> activeEconomyOverlayScopes;

        private bool mapActive;
        private bool currentMapIsSave;
        private readonly Dictionary<int, PreplacedIdentity> mapLoadBuildingIdentities =
            new Dictionary<int, PreplacedIdentity>();
        private readonly HashSet<int> mapLoadWallTiles = new HashSet<int>();
        private bool preAivBaselineCaptured;
        private readonly Dictionary<int, PreplacedIdentity> preplacedBuildings =
            new Dictionary<int, PreplacedIdentity>();
        private readonly Dictionary<int, WallTileBaseline> wallBaselines =
            new Dictionary<int, WallTileBaseline>();

        private int lastLegacyCopyMapVersion = -1;
        private int[] lastLegacyCopySourceBefore;
        private int[] lastLegacyCopyDestinationBefore;
        private int[] lastLegacyCopySource;
        private int[] lastLegacyCopyDestination;
        private List<BuildingSnapshot> legacyCopyBuildings = new List<BuildingSnapshot>();
        private readonly HashSet<int> damageActivatedTimerOwners = new HashSet<int>();

        private readonly HashSet<int> dirtyBreachPlayers = new HashSet<int>();
        private readonly Dictionary<int, int> pendingRelevantRemovals =
            new Dictionary<int, int>();
        private readonly HashSet<int> economyFixEligiblePlayers = new HashSet<int>();
        private readonly Dictionary<int, EconomyOverlayCache> economyOverlayCaches =
            new Dictionary<int, EconomyOverlayCache>();
        private readonly byte[] overlayOriginalValues = new byte[EconomyGridCellCount];
        private readonly int[] overlayChangedIndices = new int[EconomyGridCellCount];
        private readonly EconomyGridOverlayScope overlayScratchScope =
            new EconomyGridOverlayScope();
        private readonly HashSet<string> emittedInvalidPclAccesses =
            new HashSet<string>(StringComparer.Ordinal);
        private bool overlayScratchInUse;
        private bool economyFixEnabled = true;
        private bool economyFixFailureLogged;
        private bool economyProfileResolved;
        private bool economyMapRelevant;
        private int economyTopologyRevision;

        public PreplacedTestRuntime(ManualLogSource log) => this.log = log ?? throw new ArgumentNullException(nameof(log));

        public void InstallEventHandlers()
        {
            subscriptions.Add(Shared.MissionEvents.Loading.Subscribe(a => OnMapLoad(a)));
            subscriptions.Add(Shared.MissionEvents.NativeStart.Subscribe(a => OnMapStart(a)));
            subscriptions.Add(Shared.MissionEvents.Ended.Subscribe(a => OnMapUnload(a)));
            subscriptions.Add(BuildingR3EventHooks.OnBuildingTileTakeDamage.Observable.Subscribe(OnBuildingDamage));
            subscriptions.Add(BuildingR3EventHooks.OnBuildingBulldoze.Observable.Subscribe(OnBuildingBulldoze));
            subscriptions.Add(BuildingR3EventHooks.OnBuildingDelete.Observable.Subscribe(OnBuildingDelete));
            Shared.DebugLogHelper.LogInfo(log, "PREPLACED_EVENTS_READY: event-driven fix lifecycle installed; no frame polling.");
        }

        public void TryInstallNativeFixes(CrusaderLibraryLoadContext context, bool hashMatches)
        {
            if (!hashMatches)
            {
                Shared.DebugLogHelper.LogWarning(log,
                    "PREPLACED_NATIVE_INCOMPLETE: native hash differs; all native fixes remain atomically disabled.");
                return;
            }
            try
            {
                Dictionary<string, int> rvas = ResolveAll(context.Memory);
                ValidateManagedLayouts();
                long pathManagerEnd = NativePathManagerRva +
                    ((long)(MaximumPortalRecordCount - 1) * PortalRecordStrideDwords +
                    PortalThirdPclOffsetDwords + 1) * sizeof(int);
                if (NativePathManagerRva < 0 || pathManagerEnd > context.Memory.Length)
                    throw new InvalidOperationException("native path-manager range is outside the image");
                if (NativePclGridRva < 0 || NativePclGridEndRva > context.Memory.Length ||
                    NativePclEntryCount != 320800)
                    throw new InvalidOperationException("audited native PCL-grid range is outside the image or has the wrong length");
                if (FarmPlacementOffsetTableRva < 0 ||
                    FarmPlacementOffsetTableRva + FarmPlacementOffsetTablePairCount * 2 * sizeof(int) > context.Memory.Length ||
                    ConstructBuildingErrorRva < 0 ||
                    ConstructBuildingErrorRva + sizeof(int) > context.Memory.Length)
                    throw new InvalidOperationException("farm placement-offset table or construction-error field is outside the audited image");
                activeLayoutIndexBase = ResolveRipAddress(context, rvas["active-layout-reference"] + 3, 3, 7);
                ulong module = unchecked((ulong)context.ModuleHandle.ToInt64());
                if (activeLayoutIndexBase != module + ActivePlayerRuntimeStateBaseRva)
                    throw new InvalidOperationException("active player runtime-state base differs from the audited serialized-record offset");
                nativeModuleBase = module;
                nativePclGrid = (ushort*)(module + NativePclGridRva);
                initializeEconomyAvailabilityNative = Marshal.GetDelegateForFunctionPointer<InitializeEconomyAvailabilityDelegate>(
                    new IntPtr(unchecked((long)(module + InitializeEconomyAvailabilityRva))));
                ValidateLegacyPlayerStateCopy(context.Memory, rvas["legacy-player-state-copy"]);
                ValidateNativeEconomyStartRanges(context.Memory);
                ValidatePlacementReachabilityRouteCall(context.Memory, RegionPairReachabilityRva);
                ValidateWoodScoreFloorHook(context.Memory, rvas["wood-search"]);
                using (var probe = new X64InlineHook(module + WoodScoreFloorHookRva,
                    WoodScoreFloorHookLength))
                {
                    if (probe.DisplacedByteCount != WoodScoreFloorHookLength)
                        throw new InvalidOperationException("RedBird displaced an unexpected wood-score hook span");
                }
                transaction = new HookTransaction(context.Region,
                    SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                    new HookTransactionOptions { FailureMode = TransactionFailureMode.RollbackAndThrow, OwnsHooks = false });
                transaction.AddDetour(allocateHook, HookTarget.FromAddress(module + (ulong)rvas["allocate"]), AllocateSpec);
                transaction.AddDetour(economyOxenHook, HookTarget.FromAddress(module + (ulong)rvas["economy-oxen"]), EconomyOxen);
                transaction.AddDetour(economyQuarryHook, HookTarget.FromAddress(module + (ulong)rvas["economy-quarry"]), EconomyQuarry);
                transaction.AddDetour(economyWoodHook, HookTarget.FromAddress(module + (ulong)rvas["economy-wood"]), EconomyWood);
                transaction.AddDetour(farmSearchHook, HookTarget.FromAddress(module + (ulong)rvas["farm-search"]), FarmSearch);
                transaction.AddDetour(resourceSearchHook, HookTarget.FromAddress(module + (ulong)rvas["resource-search"]), ResourceSearch);
                transaction.AddDetour(woodSearchHook, HookTarget.FromAddress(module + (ulong)rvas["wood-search"]), WoodSearch);
                transaction.AddContextHook(woodScoreFloorHook,
                    HookTarget.FromAddress(module + WoodScoreFloorHookRva), ApplyWoodScoreFloor,
                    new ContextHookOptions
                    {
                        Registers = X64SmartCPUContextRegs.All,
                        HookSize = WoodScoreFloorHookLength,
                        ErrorMode = CallbackErrorMode.LogAndContinue,
                        Placement = OverwrittenInstructionPlacement.BeforeCallback
                    });
                transaction.AddDetour(nearbySearchHook, HookTarget.FromAddress(module + (ulong)rvas["nearby-search"]), NearbySearch);
                transaction.AddDetour(economyGridUpdateHook, HookTarget.FromAddress(module + (ulong)rvas["economy-grid-update"]), EconomyGridUpdate);
                transaction.AddDetour(legacyPlayerStateCopyHook,
                    HookTarget.FromAddress(module + (ulong)rvas["legacy-player-state-copy"]), LegacyPlayerStateCopy);
                CommitResult result = transaction.Commit();
                if (!result.IsCompleteSuccess || !AllHooksSucceeded() ||
                    woodScoreFloorHook.Hook.DisplacedByteCount != WoodScoreFloorHookLength)
                    throw new InvalidOperationException("atomic native hook transaction was incomplete: " + result);
                Shared.DebugLogHelper.LogInfo(log,
                    $"PREPLACED_NATIVE_READY: 10 fix detours and one scoped wood-score context hook installed atomically; activeLayoutBase=0x{activeLayoutIndexBase:X}, pclRange=0x{NativePclGridRva:X}-0x{NativePclGridEndRva:X} ({NativePclEntryCount} ushorts)." );
            }
            catch (Exception ex)
            {
                // RollbackAndThrow handles commit failures; this also covers a defensive
                // post-commit handle-consistency failure before exposing native fixes.
                try { transaction?.DisableAll(); } catch { }
                activeLayoutIndexBase = 0;
                nativeModuleBase = 0;
                nativePclGrid = null;
                initializeEconomyAvailabilityNative = null;
                lastAivState = 0;
                Shared.DebugLogHelper.LogError(log,
                    $"PREPLACED_NATIVE_INCOMPLETE: signature/ABI/address validation failed; native hook set rolled back. {ex}");
            }
        }

        private Dictionary<string, int> ResolveAll(ReadOnlySpan<byte> memory)
        {
            var definitions = new[]
            {
                Def("economy-oxen", EconomyOxenPattern, EconomyOxenRva),
                Def("economy-quarry", EconomyQuarryPattern, EconomyQuarryRva),
                Def("economy-wood", EconomyWoodPattern, EconomyWoodRva),
                Def("farm-search", FarmSearchPattern, FarmSearchRva),
                Def("resource-search", ResourceSearchPattern, ResourceSearchRva),
                Def("wood-search", WoodSearchPattern, WoodSearchRva),
                Def("nearby-search", NearbySearchPattern, NearbySearchRva),
                Def("economy-grid-update", EconomyGridUpdatePattern, EconomyGridUpdateRva),
                Def("initialize-economy-availability", InitializeEconomyAvailabilityPattern, InitializeEconomyAvailabilityRva),
                Def("legacy-player-state-copy", LegacyPlayerStateCopyPattern, LegacyPlayerStateCopyRva),
                Def("allocate", AllocateSpecPattern, AllocateSpecRva),
                Def("active-layout-reference", ActiveLayoutReferencePattern, ActiveLayoutReferenceRva)
            };
            Dictionary<string, int> result = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (NativeDefinition definition in definitions)
                result.Add(definition.Name, Shared.NativePatternResolver.ResolveUnique(memory, definition.Pattern,
                    definition.Rva, true, "PreplacedTest " + definition.Name, log).Rva);
            return result;
        }

        private static NativeDefinition Def(string name, string pattern, int rva) => new NativeDefinition(name, pattern, rva);

        private static void ValidateLegacyPlayerStateCopy(ReadOnlySpan<byte> memory, int functionRva)
        {
            if (functionRva != LegacyPlayerStateCopyRva ||
                LegacyPlayerStateCopyCallSiteRva < 0 || LegacyPlayerStateCopyCallSiteRva + 5 > memory.Length ||
                memory[LegacyPlayerStateCopyCallSiteRva] != 0xE8 ||
                !memory.Slice(LegacyPlayerStateCopyCallSiteRva - 8, 8)
                    .SequenceEqual(new byte[] { 0x81, 0xFA, 0xD5, 0x00, 0x00, 0x00, 0x7D, 0x0B }))
                throw new InvalidOperationException("legacy player-state copy function or call-site bytes differ");
            int target = Shared.NativePatternResolver.ResolveRelativeTarget(memory,
                LegacyPlayerStateCopyCallSiteRva + 1, LegacyPlayerStateCopyCallSiteRva + 5);
            if (target != functionRva)
                throw new InvalidOperationException("legacy player-state copy call-site target differs");
            long sourceEnd = LegacyPlayerStateSourceRva +
                (long)SerializedPlayerRecordCount * LegacyPlayerStateStride;
            long destinationEnd = CurrentPlayerStateDestinationRva +
                (long)SerializedPlayerRecordCount * PlayerRuntimeStateStride;
            if (sourceEnd > memory.Length || destinationEnd > memory.Length ||
                MapFormatVersionRva + sizeof(int) > memory.Length ||
                SerializedCrushedCounterOffset + sizeof(int) > LegacyPlayerStateStride)
                throw new InvalidOperationException("legacy player-state copy data ranges differ");
        }

        private static void ValidateManagedLayouts()
        {
            ValidateOffset(typeof(GameBuilding), nameof(GameBuilding.r_AliveState), 0xD0);
            ValidateOffset(typeof(GameBuilding), nameof(GameBuilding.r_BuildingType), 0xD2);
            ValidateOffset(typeof(GameBuilding), nameof(GameBuilding.r_PlayerIdOwner), 0xD6);
            ValidateOffset(typeof(GameBuilding), nameof(GameBuilding.r_GlobalId), 0xD8);
            ValidateOffset(typeof(GameBuilding), nameof(GameBuilding.r_AccessTilePositionX), 0xFE);
            ValidateOffset(typeof(GameBuilding), nameof(GameBuilding.r_AccessTilePositionY), 0x100);
            ValidateOffset(typeof(GameBuilding), nameof(GameBuilding.r_IsSleeping), 0x296);
            ValidateOffset(typeof(GameBuilding), nameof(GameBuilding.r_GatehouseId), 0x2D2);
            ValidateOffset(typeof(GamePlayerResources), nameof(GamePlayerResources.r_KeepTileId), 0xA0);
            ValidateOffset(typeof(PathConnectionRecord), nameof(PathConnectionRecord.r_RecordGlobalId), 0x08);
            ValidateOffset(typeof(PathConnectionRecord), nameof(PathConnectionRecord.r_BuildingId), 0x0C);
            ValidateOffset(typeof(PathConnectionRecord), nameof(PathConnectionRecord.r_SubjectGlobalId), 0x14);
            ValidateOffset(typeof(PathConnectionRecord), nameof(PathConnectionRecord.r_IsEnabledOrOpen), 0x18);
            ValidateOffset(typeof(PathConnectionRecord), nameof(PathConnectionRecord.r_EntryTileId), 0x24);
            ValidateOffset(typeof(PathConnectionRecord), nameof(PathConnectionRecord.r_ExitTileId), 0x30);
            ValidateOffset(typeof(PathConnectionRecord), nameof(PathConnectionRecord.r_PathComponentA), 0x34);
            ValidateOffset(typeof(PathConnectionRecord), nameof(PathConnectionRecord.r_PathComponentB), 0x38);
            ValidateOffset(typeof(PathConnectionRecord), nameof(PathConnectionRecord.r_OwnerOrAccessPlayerId), 0x1E4);
            ValidateOffset(typeof(PathConnectionRecord), nameof(PathConnectionRecord.r_PathComponentC), 0x1E8);
            ValidateSize(typeof(GameBuilding), 0x32C);
            ValidateSize(typeof(GamePlayerResources), PlayerRuntimeStateStride);
            ValidateSize(typeof(PathConnectionRecord), 0x204);
            // RVA 0xC3BF0 places native mode 0 on the stack before calling 0xE2610.
            if ((int)PathConnectionQueryMode.ExcludeLadderClimb != 0 ||
                (int)PathConnectionQueryMode.IncludeAll != 1 ||
                (int)PathConnectionQueryMode.LadderClimbOnly != 2)
                throw new InvalidOperationException("PathConnectionQueryMode no longer matches the audited E2610 ABI.");
        }

        private static void ValidateOffset(Type type, string field, int expected)
        {
            int actual = Marshal.OffsetOf(type, field).ToInt32();
            if (actual != expected)
                throw new InvalidOperationException($"managed layout mismatch: {type.Name}.{field}=0x{actual:X}, expected=0x{expected:X}");
        }

        private static void ValidateSize(Type type, int expected)
        {
            int actual = Marshal.SizeOf(type);
            if (actual != expected)
                throw new InvalidOperationException($"managed layout mismatch: sizeof({type.Name})=0x{actual:X}, expected=0x{expected:X}");
        }

        private static ulong ResolveRipAddress(CrusaderLibraryLoadContext context, int instructionRva, int displacementOffset, int length)
        {
            int targetRva = Shared.NativePatternResolver.ResolveRelativeTarget(context.Memory,
                instructionRva + displacementOffset, instructionRva + length);
            if (targetRva < 0 || targetRva >= context.Memory.Length)
                throw new InvalidOperationException("RIP-relative active-layout target is outside the image.");
            return unchecked((ulong)context.ModuleHandle.ToInt64()) + (ulong)targetRva;
        }

        private bool AllHooksSucceeded() => allocateHook.Success && economyOxenHook.Success &&
            economyQuarryHook.Success && economyWoodHook.Success && farmSearchHook.Success &&
            resourceSearchHook.Success && woodSearchHook.Success && nearbySearchHook.Success &&
            economyGridUpdateHook.Success && legacyPlayerStateCopyHook.Success && woodScoreFloorHook.Success &&
            initializeEconomyAvailabilityNative != null;

        private void EconomyGridUpdate(ulong state, int mode)
        {
            lastAivState = state;
            economyGridUpdateHook.Original(state, mode);
            if (economyProfileResolved && economyMapRelevant && mode != 0)
            {
                InvalidateEconomyOverlayCaches("economy-grid-full-rebuild");
                foreach (KeyValuePair<int, WallTileBaseline> entry in wallBaselines)
                {
                    if (entry.Value.LostWallTiles.Count != 0)
                        dirtyBreachPlayers.Add(entry.Key);
                }
            }
        }

        private short[] CaptureEconomyAvailabilityFields(int playerId)
        {
            var result = new short[10];
            for (int index = 0; index < result.Length; index++)
                result[index] = ReadPlayerInt16(playerId, WoodSearchCooldownRelativeOffset, index);
            return result;
        }

        private void LegacyPlayerStateCopy()
        {
            int mapVersion = -1;
            int[] sourceBefore = null;
            int[] destinationBefore = null;
            List<BuildingSnapshot> buildingsAtTransfer = null;
            Safe(() =>
            {
                mapVersion = *(int*)(nativeModuleBase + MapFormatVersionRva);
                sourceBefore = CaptureSerializedCrushedCounters(LegacyPlayerStateSourceRva, LegacyPlayerStateStride);
                destinationBefore = CaptureSerializedCrushedCounters(CurrentPlayerStateDestinationRva,
                    PlayerRuntimeStateStride);
                // Owner and stable identity are captured before Vanilla can run later
                // initialization passes that rewrite decorative ruin ownership.
                buildingsAtTransfer = CaptureDestroyedTowers();
            });

            // Passive detour: Vanilla is always called exactly once, even if capture failed.
            legacyPlayerStateCopyHook.Original();

            Safe(() =>
            {
                int[] sourceAfter = CaptureSerializedCrushedCounters(LegacyPlayerStateSourceRva,
                    LegacyPlayerStateStride);
                int[] destinationAfter = CaptureSerializedCrushedCounters(CurrentPlayerStateDestinationRva,
                    PlayerRuntimeStateStride);
                lastLegacyCopyMapVersion = mapVersion;
                lastLegacyCopySourceBefore = sourceBefore;
                lastLegacyCopyDestinationBefore = destinationBefore;
                lastLegacyCopySource = sourceAfter;
                lastLegacyCopyDestination = destinationAfter;
                legacyCopyBuildings = buildingsAtTransfer ?? new List<BuildingSnapshot>();
            });
        }

        private int[] CaptureSerializedCrushedCounters(int baseRva, int stride)
        {
            if (nativeModuleBase == 0) throw new InvalidOperationException("native module base unavailable");
            var result = new int[SerializedPlayerRecordCount];
            byte* baseAddress = (byte*)(nativeModuleBase + (ulong)baseRva);
            for (int playerIndex = 0; playerIndex < result.Length; playerIndex++)
                result[playerIndex] = *(int*)(baseAddress + playerIndex * stride + SerializedCrushedCounterOffset);
            return result;
        }

        private void ApplyLegacyRuinTimerFix()
        {
            if (lastLegacyCopySourceBefore == null || lastLegacyCopyDestinationBefore == null ||
                lastLegacyCopySource == null || lastLegacyCopyDestination == null || activeLayoutIndexBase == 0)
            {
                Shared.DebugLogHelper.LogInfo(log,
                    $"PREPLACED_LEGACY_TIMER_FIX_SKIPPED: sequence={mapSequence}; reason=no-complete-legacy-transfer-capture.");
                return;
            }

            for (int playerId = 1; playerId <= MaxPlayablePlayerId; playerId++)
            {
                string transfer = legacyRuinTimerFix.ClassifyTransfer(IsAi(playerId), currentMapIsSave,
                    lastLegacyCopyMapVersion, LegacyPlayerStateCopyVersionExclusive,
                    lastLegacyCopySourceBefore[playerId], lastLegacyCopySource[playerId],
                    lastLegacyCopyDestinationBefore[playerId], lastLegacyCopyDestination[playerId]);
                BuildingSnapshot[] matchingTowers = legacyCopyBuildings
                    .Where(building => building.OwnerId == playerId && building.Id > 0 &&
                        IsDestroyedTower(building.Type))
                    .GroupBy(building => building.GlobalId != 0
                        ? "global=" + building.GlobalId
                        : "game=" + building.Id + "/type=" + building.Type)
                    .Select(group => group.First())
                    .ToArray();
                int currentTimer = ReadPlayerGlobal(playerId, CrushedCounterRelativeOffset);
                string decision = legacyRuinTimerFix.ClassifyApplication(transfer,
                    matchingTowers.Length != 0, damageActivatedTimerOwners.Contains(playerId), currentTimer);
                if (!legacyRuinTimerFix.IsApplicationEligible(decision))
                {
                    if (legacyRuinTimerFix.IsTransferEligible(transfer))
                        Shared.DebugLogHelper.LogInfo(log,
                            $"PREPLACED_LEGACY_TIMER_FIX_SKIPPED: sequence={mapSequence}; player={playerId}; " +
                            $"reason={decision}; currentTimer={currentTimer}; matchingDestroyedTowers={matchingTowers.Length}.");
                    continue;
                }

                int* timer = (int*)(activeLayoutIndexBase +
                    (ulong)(playerId * PlayerRuntimeStateStride + CrushedCounterRelativeOffset));
                *timer = 0;
                int verified = *timer;
                if (verified != 0)
                    throw new InvalidOperationException("The eligible legacy crushed-building timer could not be normalized.");
                Shared.DebugLogHelper.LogInfo(log, "PREPLACED_LEGACY_TIMER_FIX_APPLIED: " +
                    $"sequence={mapSequence}; player={playerId}; timer=1->0; mapVersion={lastLegacyCopyMapVersion}; " +
                    $"matchingDestroyedTowers=[{string.Join(",", matchingTowers.Select(value => value.Id + "/" +
                        value.GlobalId + "/" + value.Type + "/ownerAtTransfer=" + value.OwnerId))}]; " +
                    "serializedSourceUnchanged=true; buildingRecordsUnchanged=true");
            }
        }

        private int AllocateSpec(ulong state, int playerId)
        {
            lastAivState = state;
            Safe(() => CaptureMapLoadBuildingIdentities("first-allocate-spec.pre"));
            return allocateHook.Original(state, playerId);
        }

        private void EconomyOxen(ulong state, int playerId) => RunEconomyPlayer(
            economyOxenHook, state, playerId, "oxen", eStructs.STRUCT_OXEN_BASE);

        private void EconomyQuarry(ulong state, int playerId) => RunEconomyPlayer(
            economyQuarryHook, state, playerId, "quarry", eStructs.STRUCT_QUARRY);

        private void EconomyWood(ulong state, int playerId) => RunEconomyPlayer(
            economyWoodHook, state, playerId, "wood", eStructs.STRUCT_WOODCUTTERS_HUT);

        private void RunEconomyPlayer(DetourHandle<EconomyPlayerDelegate> hook, ulong state, int playerId,
            string phase, eStructs desiredType)
        {
            int previousPlayer = nearbyEconomyPlayerId;
            ulong previousState = nearbyEconomyState;
            nearbyEconomyPlayerId = playerId;
            nearbyEconomyState = state;
            try { hook.Original(state, playerId); }
            finally
            {
                nearbyEconomyPlayerId = previousPlayer;
                nearbyEconomyState = previousState;
            }
        }

        private long FarmSearch(ulong state, int playerId, int desiredStructureType)
        {
            if (!economyMapRelevant)
                return farmSearchHook.Original(state, playerId, desiredStructureType);
            TryActivateOrRefreshEconomyFix(state, playerId, "farm-search");
            EconomyGridOverlayScope overlay = EnterEconomyOverlay(state, playerId, "farm-search");
            try { return farmSearchHook.Original(state, playerId, desiredStructureType); }
            finally { ExitEconomyOverlay(overlay); }
        }

        private void ResourceSearch(ulong state, int playerId, int mode)
        {
            if (!economyMapRelevant)
            {
                resourceSearchHook.Original(state, playerId, mode);
                return;
            }
            TryActivateOrRefreshEconomyFix(state, playerId, "resource-search");
            EconomyGridOverlayScope overlay = EnterEconomyOverlay(state, playerId, "resource-search");
            try { resourceSearchHook.Original(state, playerId, mode); }
            finally { ExitEconomyOverlay(overlay); }
        }

        private void WoodSearch(ulong state, int playerId)
        {
            if (!economyMapRelevant)
            {
                woodSearchHook.Original(state, playerId);
                return;
            }
            TryActivateOrRefreshEconomyFix(state, playerId, "wood-search");
            EconomyGridOverlayScope overlay = EnterEconomyOverlay(state, playerId, "wood-search");
            try { woodSearchHook.Original(state, playerId); }
            finally { ExitEconomyOverlay(overlay); }
        }

        private void NearbySearch(ulong state, uint coarseX, uint coarseY)
        {
            if (!economyMapRelevant)
            {
                nearbySearchHook.Original(state, coarseX, coarseY);
                return;
            }
            int playerId = nearbyEconomyState == state ? nearbyEconomyPlayerId : 0;
            if (IsAi(playerId))
            {
                TryActivateOrRefreshEconomyFix(state, playerId, "nearby-search");
                EconomyGridOverlayScope overlay = EnterEconomyOverlay(state, playerId, "nearby-search");
                try { nearbySearchHook.Original(state, coarseX, coarseY); }
                finally { ExitEconomyOverlay(overlay); }
                return;
            }
            nearbySearchHook.Original(state, coarseX, coarseY);
        }

        private EconomyGridOverlayScope EnterEconomyOverlay(ulong state, int playerId, string helper)
        {
            EconomyGridOverlayScope overlay;
            try
            {
                overlay = TryApplyEconomyGridOverlay(state, playerId, helper, false);
            }
            catch (Exception ex)
            {
                DisableEconomyFix("overlay-prepare", ex);
                return null;
            }
            if (overlay == null) return null;
            if (activeEconomyOverlayScopes == null)
                activeEconomyOverlayScopes = new Stack<EconomyGridOverlayScope>();
            activeEconomyOverlayScopes.Push(overlay);
            return overlay;
        }

        private void ExitEconomyOverlay(EconomyGridOverlayScope overlay)
        {
            if (overlay == null) return;
            if (activeEconomyOverlayScopes == null || activeEconomyOverlayScopes.Count == 0 ||
                !ReferenceEquals(activeEconomyOverlayScopes.Peek(), overlay))
            {
                DisableEconomyFix("overlay-context-restore",
                    new InvalidOperationException("The economy-overlay context stack was corrupted."));
            }
            else activeEconomyOverlayScopes.Pop();
            RestoreEconomyGridOverlay(overlay);
        }

        private static void ValidatePlacementReachabilityRouteCall(ReadOnlySpan<byte> memory,
            int regionPairReachabilityRva)
        {
            const int blockStartRva = PlacementReachabilityRouteCallSiteRva - 0x0F;
            byte[] expected =
            {
                0x48, 0x8D, 0x0D, 0x0B, 0x9A, 0xFE, 0x05,
                0xC7, 0x44, 0x24, 0x20, 0x00, 0x00, 0x00, 0x00,
                0xE8, 0xAE, 0xE9, 0x01, 0x00, 0x85, 0xC0, 0x75, 0x05
            };
            if (blockStartRva < 0 || blockStartRva + expected.Length > memory.Length ||
                !memory.Slice(blockStartRva, expected.Length).SequenceEqual(expected))
                throw new InvalidOperationException("C3BF0 route-call argument block differs from the audited instructions");
            int target = Shared.NativePatternResolver.ResolveRelativeTarget(memory,
                PlacementReachabilityRouteCallSiteRva + 1, PlacementReachabilityRouteCallSiteRva + 5);
            if (target != regionPairReachabilityRva)
                throw new InvalidOperationException("C3BF0 route-call target differs from E2610");
        }

        private void TryActivateOrRefreshEconomyFix(ulong state, int playerId, string helper)
        {
            if (!economyFixEnabled || reconcilingEconomyAvailability || !economyProfileResolved ||
                !economyMapRelevant || currentMapIsSave || state == 0 || !IsAi(playerId))
                return;
            try
            {
                if (dirtyBreachPlayers.Contains(playerId))
                    TryConfirmDirtyWallBreach(playerId);
                PlayerSession session = Session(playerId);
                EconomyFixActivationState activationState = session.EconomyFixState;
                if (activationState == EconomyFixActivationState.None) return;

                if (activationState == EconomyFixActivationState.ActivePortal ||
                    activationState == EconomyFixActivationState.ActiveBreach)
                {
                    if (session.EconomyFixValidatedRevision == economyTopologyRevision) return;
                    bool stillValid = activationState == EconomyFixActivationState.ActivePortal
                        ? HasParticipatingFriendlyBaselinePortal(playerId, true)
                        : HasConfirmedBreachEconomyAccess(playerId);
                    if (stillValid)
                    {
                        session.EconomyFixValidatedRevision = economyTopologyRevision;
                        return;
                    }
                    economyFixEligiblePlayers.Remove(playerId);
                    session.EconomyFixState = EconomyFixActivationState.Suspended;
                    EmitEconomyFixState("SUSPENDED", session, helper,
                        activationState == EconomyFixActivationState.ActivePortal
                            ? "preplaced-portal-route-unavailable"
                            : "confirmed-breach-route-unavailable");
                    activationState = EconomyFixActivationState.Suspended;
                }

                if (activationState == EconomyFixActivationState.Suspended)
                {
                    activationState = economyAccessFix.Resume(session.ConfirmedWallBreach,
                        HasFriendlyPreplacedPortalBuilding(playerId), session.WallAccessRole);
                    session.EconomyFixState = activationState;
                    if (activationState == EconomyFixActivationState.None) return;
                    EmitEconomyFixState("PENDING", session, helper, "topology-recheck");
                }

                bool accessReady;
                string cause;
                if (activationState == EconomyFixActivationState.PendingPortal)
                {
                    cause = "preplaced-friendly-portal";
                    accessReady = HasParticipatingFriendlyBaselinePortal(playerId, true);
                }
                else if (activationState == EconomyFixActivationState.PendingBreach)
                {
                    cause = "confirmed-wall-breach";
                    accessReady = HasConfirmedBreachEconomyAccess(playerId);
                }
                else return;

                if (!accessReady)
                {
                    if (!session.MatchesWaitState(activationState, economyTopologyRevision, false))
                    {
                        session.SetWaitState(activationState, economyTopologyRevision, false);
                        EmitEconomyFixState("WAITING_FOR_ROUTE", session, helper,
                            activationState == EconomyFixActivationState.PendingPortal
                                ? "native-portal-graph-not-ready"
                                : session.ConfirmedWallBreach
                                    ? "breach-access-not-currently-reachable"
                                    : "wall-breach-not-confirmed");
                    }
                    return;
                }

                if (!ReconcileEconomyAvailability(state, session, helper, cause))
                {
                    if (!session.MatchesWaitState(activationState, economyTopologyRevision, true))
                    {
                        session.SetWaitState(activationState, economyTopologyRevision, true);
                        EmitEconomyFixState("WAITING_FOR_ROUTE", session, helper,
                            "census-overlay-unavailable-or-no-byte04-change");
                    }
                    return;
                }
                session.EconomyFixState = economyAccessFix.Activate(activationState);
                session.EconomyFixActivationEpoch++;
                session.EconomyFixValidatedRevision = economyTopologyRevision;
                session.ClearWaitState();
                economyFixEligiblePlayers.Add(playerId);
                EmitEconomyFixState("ACTIVATED", session, helper, cause);
            }
            catch (Exception ex)
            {
                DisableEconomyFix("activation-or-refresh", ex);
            }
        }

        private void TryConfirmDirtyWallBreach(int playerId)
        {
            if (!players.TryGetValue(playerId, out PlayerSession session) || session.ConfirmedWallBreach ||
                !wallBaselines.TryGetValue(playerId, out WallTileBaseline baseline))
            {
                dirtyBreachPlayers.Remove(playerId);
                return;
            }

            WallAnchorPair confirmed = null;
            foreach (WallAnchorPair anchor in baseline.Anchors)
            {
                if (!baseline.LostWallTiles.Contains(anchor.WallTileId)) continue;
                if (!economyAccessFix.IsConfirmedBreach(true, anchor.OldInsidePcl, anchor.OldOutsidePcl,
                    CurrentPcl(anchor.InsideTileId), CurrentPcl(anchor.OutsideTileId))) continue;
                if (!HasLostWallAnchorEconomyAccess(playerId, baseline, anchor)) continue;
                confirmed = anchor;
                break;
            }
            if (confirmed == null) return;

            session.ConfirmedWallBreach = true;
            economyFixEligiblePlayers.Remove(playerId);
            session.EconomyFixState = currentMapIsSave
                ? EconomyFixActivationState.None
                : EconomyFixActivationState.PendingBreach;
            session.EconomyFixValidatedRevision = -1;
            session.ClearWaitState();
            dirtyBreachPlayers.Remove(playerId);
            InvalidateEconomyOverlayCaches("confirmed-wall-breach-player-" + playerId);
            Shared.DebugLogHelper.LogInfo(log,
                $"PREPLACED_CONFIRMED_WALL_BREACH: sequence={mapSequence}; player={playerId}; " +
                $"wallTile={confirmed.WallTileId}; insideTile={confirmed.InsideTileId}; " +
                $"outsideTile={confirmed.OutsideTileId}; pcl={CurrentPcl(confirmed.InsideTileId)}.");
            EmitEconomyFixState("PENDING", session, "economy-search",
                "confirmed-wall-breach-awaiting-reconciliation");
        }

        private bool ReconcileEconomyAvailability(ulong state, PlayerSession session, string helper, string cause)
        {
            EconomyGridOverlayScope overlay = null;
            short[] before = CaptureEconomyAvailabilityFields(session.PlayerId);
            short[] after = null;
            try
            {
                overlay = TryApplyEconomyGridOverlay(state, session.PlayerId,
                    "economy-census-reconcile-" + cause, true);
                if (overlay == null) return false;
                reconcilingEconomyAvailability = true;
                if (initializeEconomyAvailabilityNative == null)
                    throw new InvalidOperationException("The validated economy-census delegate is unavailable.");
                initializeEconomyAvailabilityNative(state, session.PlayerId);
                after = CaptureEconomyAvailabilityFields(session.PlayerId);
            }
            finally
            {
                reconcilingEconomyAvailability = false;
                if (overlay != null) RestoreEconomyGridOverlay(overlay);
            }
            if (!economyFixEnabled || overlay == null || after == null) return false;

            string fields = string.Join(",", before.Select((value, index) =>
                $"+0x{WoodSearchCooldownRelativeOffset + index * sizeof(short):X}={value}->{after[index]}"));
            Shared.DebugLogHelper.LogInfo(log,
                $"PREPLACED_ECONOMY_CENSUS_RECONCILED: sequence={mapSequence}; player={session.PlayerId}; " +
                $"cause={cause}; trigger={helper}; nativeStart={DescribeNativeEconomyStart(session.PlayerId)}; " +
                $"keepPcl={overlay.KeepPcl}; reachablePcls=[{string.Join(",", overlay.ReachablePcls)}]; " +
                $"changedCells={overlay.ChangedCells}; fields=[{fields}]; restoredExactly=true.");
            return true;
        }

        private bool HasConfirmedBreachEconomyAccess(int playerId)
        {
            if (!players.TryGetValue(playerId, out PlayerSession session) || !session.ConfirmedWallBreach ||
                !wallBaselines.TryGetValue(playerId, out WallTileBaseline baseline) ||
                !baseline.LostWallTiles.Any(baseline.ComponentTiles.Contains) ||
                !TryResolveNativeReachablePcls(playerId, out _, out HashSet<int> reachablePcls, out _))
                return false;
            return baseline.Anchors.Any(anchor =>
            {
                if (!baseline.LostWallTiles.Contains(anchor.WallTileId)) return false;
                int insidePcl = CurrentPcl(anchor.InsideTileId);
                int outsidePcl = CurrentPcl(anchor.OutsideTileId);
                return insidePcl > 0 && outsidePcl > 0 &&
                    (insidePcl == outsidePcl || reachablePcls.Contains(outsidePcl)) &&
                    reachablePcls.Contains(insidePcl);
            });
        }

        private bool HasFriendlyPreplacedPortalBuilding(int playerId)
        {
            if (!wallBaselines.TryGetValue(playerId, out WallTileBaseline baseline)) return false;
            foreach (PreplacedIdentity identity in preplacedBuildings.Values)
            {
                if (!IsPortalStructure((eStructs)identity.StructureType) ||
                    (identity.OwnerId != playerId && !IsAllied(playerId, identity.OwnerId)) ||
                    !TryCaptureBuilding(identity.BuildingId, out BuildingSnapshot building) ||
                    !identity.MatchesStableRecord(building.Id, building.GlobalId, (int)building.Type) ||
                    !IsLiving(building)) continue;
                if (BuildingFootprintOverlaps(building, baseline.ComponentBlockers)) return true;
            }
            return false;
        }

        private string DescribeNativeEconomyStart(int playerId) =>
            TryGetNativeEconomyStart(playerId, out int x, out int y)
                ? $"({x},{y})/coarse=({x / EconomyCoarseCellTileSize},{y / EconomyCoarseCellTileSize})"
                : "unavailable";

        private void EmitEconomyFixState(string transition, PlayerSession session, string helper, string reason)
        {
            string route = TryGetEconomyOverlayCache(session.PlayerId, out EconomyOverlayCache cache)
                ? $"keepPcl={cache.StartPcl}; presentPcls={cache.PresentPclCount}; reachablePcls=[{string.Join(",", cache.ReachablePcls)}]"
                : "route=unresolved";
            Shared.DebugLogHelper.LogInfo(log,
                $"PREPLACED_ECONOMY_FIX_{transition}: sequence={mapSequence}; player={session.PlayerId}; " +
                $"state={session.EconomyFixState}; epoch={session.EconomyFixActivationEpoch}; trigger={helper}; " +
                $"reason={reason}; nativeStart={DescribeNativeEconomyStart(session.PlayerId)}; {route}.");
        }

        private void ApplyWoodScoreFloor(NativePointer<X64SmartCPUContext> context)
        {
            try
            {
                if (!economyFixEnabled || activeEconomyOverlayScopes == null ||
                    activeEconomyOverlayScopes.Count == 0) return;
                X64SmartCPUContext* registers = context.Pointer;
                EconomyGridOverlayScope overlay = activeEconomyOverlayScopes.Peek();
                int playerId = unchecked((int)(uint)registers->RDX);
                if (overlay.State != registers->RCX || overlay.PlayerId != playerId ||
                    !string.Equals(overlay.Helper, "wood-search", StringComparison.Ordinal) ||
                    !players.TryGetValue(playerId, out PlayerSession session) ||
                    (session.EconomyFixState != EconomyFixActivationState.ActivePortal &&
                     session.EconomyFixState != EconomyFixActivationState.ActiveBreach)) return;

                registers->RBX = unchecked((ulong)(uint)int.MinValue);
                overlay.WoodScoreFloorApplied = true;
            }
            catch (Exception ex)
            {
                DisableEconomyFix("wood-score-floor-context", ex);
            }
        }

        private EconomyGridOverlayScope TryApplyEconomyGridOverlay(ulong state, int playerId, string helper,
            bool allowPendingActivation)
        {
            if (!economyFixEnabled || state == 0 || !mapActive || !IsAi(playerId))
                return null;
            if (!economyMapRelevant || (!allowPendingActivation && !economyFixEligiblePlayers.Contains(playerId)))
                return null;
            if (overlayScratchInUse)
                throw new InvalidOperationException("Nested economy overlays are not supported safely.");

            if (!TryGetEconomyOverlayCache(playerId, out EconomyOverlayCache cache))
                return null;
            byte* grid = (byte*)state + EconomyGridBaseOffset;
            byte[] projected = cache.Projected;
            int changedCells = 0;
            overlayScratchInUse = true;
            try
            {
                for (int index = 0; index < EconomyGridCellCount; index++)
                {
                    byte current = grid[index * EconomyGridCellStride + 0x04];
                    if (current == projected[index]) continue;
                    overlayChangedIndices[changedCells] = index;
                    overlayOriginalValues[changedCells] = current;
                    grid[index * EconomyGridCellStride + 0x04] = projected[index];
                    changedCells++;
                }
                if (changedCells == 0)
                {
                    overlayScratchInUse = false;
                    return null;
                }
                for (int changed = 0; changed < changedCells; changed++)
                {
                    int index = overlayChangedIndices[changed];
                    if (grid[index * EconomyGridCellStride + 0x04] != projected[index])
                        throw new InvalidOperationException("The economy byte+04 overlay did not apply exactly.");
                }
            }
            catch
            {
                // Even a partial write must leave Vanilla's shared grid byte-identical.
                for (int changed = 0; changed < changedCells; changed++)
                    grid[overlayChangedIndices[changed] * EconomyGridCellStride + 0x04] = overlayOriginalValues[changed];
                overlayScratchInUse = false;
                throw;
            }

            overlayScratchScope.Reset(state, playerId, helper, cache.StartPcl,
                cache.ReachablePcls, cache.PresentPclCount, changedCells);
            return overlayScratchScope;
        }

        private bool HasParticipatingFriendlyBaselinePortal(int playerId, bool requireCapturedBaseline)
        {
            if (!wallBaselines.TryGetValue(playerId, out WallTileBaseline baseline)) return false;
            var portals = new List<BuildingSnapshot>();
            foreach (PreplacedIdentity identity in preplacedBuildings.Values)
            {
                if (!IsPortalStructure((eStructs)identity.StructureType) ||
                    !TryCaptureBuilding(identity.BuildingId, out BuildingSnapshot building) ||
                    !identity.MatchesStableRecord(building.Id, building.GlobalId, (int)building.Type) ||
                    !IsLiving(building) ||
                    (building.OwnerId != playerId && !IsAllied(playerId, building.OwnerId)) ||
                    !BuildingFootprintOverlaps(building, baseline.ComponentBlockers)) continue;
                portals.Add(building);
            }
            if (portals.Count == 0 || !TryGetEconomyOverlayCache(playerId, out EconomyOverlayCache cache) ||
                cache.ReachablePcls.Length <= 1)
                return false;
            int startPcl = cache.StartPcl;
            int[] reachablePcls = cache.ReachablePcls;

            var portalEdges = new List<FriendlyPortalEdge>();
            foreach (BuildingSnapshot portal in portals)
            {
                if (!GamePathingManagerAPI.Instance.TryGetPathConnectionRecordByBuildingId(
                        portal.Id, out PathConnectionRecord* record) || record == null ||
                    record->r_IsActive == 0 || record->r_ConnectionClass == PathConnectionClass.LadderClimb ||
                    !TryGetPclByTileId(record->r_EntryTileId, out int first, "eligibility-entry") ||
                    !TryGetPclByTileId(record->r_ExitTileId, out int second, "eligibility-exit"))
                    continue;
                if (first > 0 && second > 0 && first != second)
                    portalEdges.Add(new FriendlyPortalEdge(first, second));
            }
            if (portalEdges.Count == 0) return false;

            // FindNextComponentTowardDestination is the authoritative Vanilla route
            // decision. A portal is relevant only if one of its PCL edges occurs on
            // an actual ExcludeLadderClimb chain from the keep to a reachable target.
            GamePathingManagerAPI pathing = GamePathingManagerAPI.Instance;
            bool wasResolvingRoutes = resolvingEconomyOverlayRoutes;
            resolvingEconomyOverlayRoutes = true;
            try
            {
                foreach (int targetPcl in reachablePcls)
                {
                    if (targetPcl == startPcl) continue;
                    int currentPcl = startPcl;
                    var seen = new HashSet<int> { currentPcl };
                    while (currentPcl != targetPcl && seen.Count <= reachablePcls.Length)
                    {
                        int nextPcl = pathing.FindNextComponentTowardDestination(playerId,
                            currentPcl, targetPcl, PathConnectionQueryMode.ExcludeLadderClimb);
                        if (nextPcl <= 0 || nextPcl == currentPcl || !seen.Add(nextPcl)) break;
                        if (portalEdges.Any(edge => edge.Connects(currentPcl, nextPcl))) return true;
                        currentPcl = nextPcl;
                    }
                }
            }
            finally
            {
                resolvingEconomyOverlayRoutes = wasResolvingRoutes;
            }
            return false;
        }

        private static void ValidateNativeEconomyStartRanges(ReadOnlySpan<byte> memory)
        {
            long lastPlayerOffset = (long)MaxPlayablePlayerId * PlayerRuntimeStateStride;
            if (NativeEconomyStartXRva < 0 || NativeEconomyStartYRva != NativeEconomyStartXRva + sizeof(int) ||
                NativeEconomyStartYRva + lastPlayerOffset + sizeof(int) > memory.Length ||
                AlternativeOpenAreaSearchRva < 0 || AlternativeOpenAreaSearchRva >= memory.Length ||
                AivReachableOpenAreaSearchRva < 0 || AivReachableOpenAreaSearchRva >= memory.Length)
                throw new InvalidOperationException("native per-player economy-start coordinate range differs");
        }

        private static void ValidateWoodScoreFloorHook(ReadOnlySpan<byte> memory, int woodSearchRva)
        {
            byte[] expected =
            {
                0x48, 0x63, 0xC2,
                0x4C, 0x69, 0xC8, 0x3C, 0x58, 0x00, 0x00,
                0xB8, 0x67, 0x66, 0x66, 0x66
            };
            if (woodSearchRva != WoodSearchRva || expected.Length != WoodScoreFloorHookLength ||
                WoodScoreFloorHookRva < WoodSearchRva ||
                WoodScoreFloorHookRva + WoodScoreFloorHookLength > WoodSearchRva + 0x371 ||
                WoodScoreFloorHookRva + WoodScoreFloorHookLength > memory.Length ||
                !memory.Slice(WoodScoreFloorHookRva, WoodScoreFloorHookLength).SequenceEqual(expected))
                throw new InvalidOperationException("wood-score hook bytes, boundary, or function contract differ");
        }

        private bool TryGetEconomyOverlayCache(int playerId, out EconomyOverlayCache cache)
        {
            cache = null;
            ulong accessSignature = ComputeEconomyAccessSignature(playerId);
            if (economyOverlayCaches.TryGetValue(playerId, out EconomyOverlayCache existing) &&
                existing.Revision == economyTopologyRevision &&
                existing.AccessSignature == accessSignature)
            {
                cache = existing;
                return true;
            }
            if (!TryResolveNativeReachablePcls(playerId, out int startPcl,
                    out HashSet<int> reachablePcls, out int presentPclCount))
                return false;

            var projected = new byte[EconomyGridCellCount];
            for (int index = 0; index < EconomyGridCellCount; index++)
            {
                projected[index] = checked((byte)CountPclTilesOutsideSet(
                    index / EconomyGridWidth, index % EconomyGridWidth, reachablePcls));
            }
            cache = new EconomyOverlayCache(economyTopologyRevision, accessSignature, startPcl,
                reachablePcls.OrderBy(value => value).ToArray(), presentPclCount, projected);
            economyOverlayCaches[playerId] = cache;
            return true;
        }

        private ulong ComputeEconomyAccessSignature(int playerId)
        {
            ulong hash = 1469598103934665603UL;
            for (int otherPlayerId = 1; otherPlayerId <= MaxPlayablePlayerId; otherPlayerId++)
                hash = Hash(hash, IsAllied(playerId, otherPlayerId) ? 1 : 0);
            var records = GamePathingManagerAPI.Instance.GetPathConnectionArray();
            for (int spanIndex = 0; spanIndex < records.Length; spanIndex++)
            {
                PathConnectionRecord* record = records.GetValuePointer(spanIndex);
                if (record == null || record->r_IsActive == 0) continue;
                hash = Hash(hash, spanIndex);
                hash = Hash(hash, unchecked((int)record->r_RecordGlobalId));
                hash = Hash(hash, record->r_BuildingId);
                hash = Hash(hash, record->r_IsEnabledOrOpen);
                hash = Hash(hash, (int)record->r_ConnectionClass);
                hash = Hash(hash, record->r_EntryTileId);
                hash = Hash(hash, record->r_ExitTileId);
                hash = Hash(hash, record->r_PathComponentA);
                hash = Hash(hash, record->r_PathComponentB);
                hash = Hash(hash, record->r_PathComponentC);
                hash = Hash(hash, record->r_OwnerOrAccessPlayerId);
                hash = Hash(hash, TryGetPclByTileId(record->r_EntryTileId, out int entryPcl,
                    "economy-access-signature-entry") ? entryPcl : 0);
                hash = Hash(hash, TryGetPclByTileId(record->r_ExitTileId, out int exitPcl,
                    "economy-access-signature-exit") ? exitPcl : 0);
            }
            return hash;
        }

        private bool TryResolveNativeReachablePcls(
            int playerId,
            out int keepPcl,
            out HashSet<int> reachablePcls,
            out int presentPclCount)
        {
            keepPcl = 0;
            reachablePcls = new HashSet<int>();
            presentPclCount = 0;
            // The BFS begins at AFA8/AFAC, while the downstream C3BF0 route contract
            // uses the keep tile stored at AFB0. Require both and use C3BF0's source
            // PCL for the player-specific reachability closure.
            if (!TryGetNativeEconomyStart(playerId, out int startX, out int startY) ||
                !TryGetPclAt(startX, startY, out int searchStartPcl) ||
                !TryGetKeepPcl(playerId, out keepPcl) || searchStartPcl <= 0)
                return false;

            GamePathingManagerAPI pathing = GamePathingManagerAPI.Instance;
            Span<ushort> grid = pathing.GetPathComponentGrid();
            Span<int> counts = pathing.GetNativeComponentTileCounts();
            if (grid.Length != NativePclEntryCount || counts.Length == 0 ||
                keepPcl <= 0 || keepPcl >= counts.Length)
                return false;

            var present = new bool[counts.Length];
            for (int tileIndex = 0; tileIndex < grid.Length; tileIndex++)
            {
                int pcl = grid[tileIndex];
                if (pcl <= 0) continue;
                if (pcl >= present.Length) return false;
                if (!present[pcl])
                {
                    present[pcl] = true;
                    presentPclCount++;
                }
            }

            reachablePcls.Add(keepPcl);
            bool wasResolvingRoutes = resolvingEconomyOverlayRoutes;
            resolvingEconomyOverlayRoutes = true;
            try
            {
                for (int pcl = 1; pcl < present.Length; pcl++)
                {
                    if (!present[pcl] || pcl == keepPcl) continue;
                    int next = pathing.FindNextComponentTowardDestination(
                        playerId, keepPcl, pcl, PathConnectionQueryMode.ExcludeLadderClimb);
                    if (next > 0) reachablePcls.Add(pcl);
                }
            }
            finally
            {
                resolvingEconomyOverlayRoutes = wasResolvingRoutes;
            }
            if (!reachablePcls.Contains(searchStartPcl)) return false;
            return true;
        }

        private void InvalidateEconomyOverlayCaches(string reason)
        {
            economyTopologyRevision = unchecked(economyTopologyRevision + 1);
            economyOverlayCaches.Clear();
        }

        private bool TryGetNativeEconomyStart(int playerId, out int x, out int y)
        {
            x = 0;
            y = 0;
            if (nativeModuleBase == 0 || !IsValidOwner(playerId)) return false;
            long playerOffset = checked((long)playerId * PlayerRuntimeStateStride);
            x = *(int*)(nativeModuleBase + (ulong)NativeEconomyStartXRva + (ulong)playerOffset);
            y = *(int*)(nativeModuleBase + (ulong)NativeEconomyStartYRva + (ulong)playerOffset);
            return GameTileManagerAPI.Instance.IsTileInsideMapBounds(x, y);
        }

        private int CurrentPcl(int tileId) => TryGetPclByTileId(tileId, out int pcl, "route-matrix-anchor") ? pcl : 0;

        private void RestoreEconomyGridOverlay(EconomyGridOverlayScope overlay)
        {
            bool restored = false;
            try
            {
                byte* grid = (byte*)overlay.State + EconomyGridBaseOffset;
                for (int changed = 0; changed < overlay.ChangedCells; changed++)
                    grid[overlayChangedIndices[changed] * EconomyGridCellStride + 0x04] = overlayOriginalValues[changed];
                restored = true;
                for (int changed = 0; changed < overlay.ChangedCells; changed++)
                {
                    int index = overlayChangedIndices[changed];
                    if (grid[index * EconomyGridCellStride + 0x04] == overlayOriginalValues[changed]) continue;
                    restored = false;
                    break;
                }
            }
            catch (Exception ex)
            {
                DisableEconomyFix("overlay-restore", ex);
            }
            finally
            {
                overlayScratchInUse = false;
            }
            if (!restored)
                DisableEconomyFix("overlay-restore-verification",
                    new InvalidOperationException("Not all 25,600 economy byte+04 values were restored."));
        }

        private void DisableEconomyFix(string stage, Exception ex)
        {
            economyFixEnabled = false;
            if (economyFixFailureLogged) return;
            economyFixFailureLogged = true;
            Shared.DebugLogHelper.LogError(log,
                $"PREPLACED_ECONOMY_FIX_DISABLED: stage={stage}; Vanilla searches will be used for the rest of this process; exception={ex}");
        }

        private int CountPclTilesOutsideSet(int coarseX, int coarseY, HashSet<int> reachablePcls)
        {
            int count = 0;
            GameTileManagerAPI api = GameTileManagerAPI.Instance;
            int beginX = coarseX * EconomyCoarseCellTileSize;
            int beginY = coarseY * EconomyCoarseCellTileSize;
            for (int dx = 0; dx < EconomyCoarseCellTileSize; dx++)
                for (int dy = 0; dy < EconomyCoarseCellTileSize; dy++)
                {
                    int x = beginX + dx;
                    int y = beginY + dy;
                    if (!api.IsTileInsideMapBounds(x, y)) { count++; continue; }
                    int tileId = api.GetTileId(x, y);
                    if (!TryGetPclByTileId(tileId, out int pcl, "shadow-economy") || !reachablePcls.Contains(pcl)) count++;
                }
            return count;
        }

        private bool HasLostWallAnchorEconomyAccess(int playerId, WallTileBaseline baseline,
            WallAnchorPair anchor)
        {
            if (anchor == null || !baseline.LostWallTiles.Contains(anchor.WallTileId) ||
                !TryResolveNativeReachablePcls(playerId, out _, out HashSet<int> reachablePcls, out _))
                return false;
            int insidePcl = CurrentPcl(anchor.InsideTileId);
            int outsidePcl = CurrentPcl(anchor.OutsideTileId);
            return insidePcl > 0 && outsidePcl > 0 && insidePcl == outsidePcl &&
                reachablePcls.Contains(insidePcl);
        }

        private static ulong Hash(ulong value, int data)
        {
            unchecked
            {
                value ^= (uint)data;
                return value * 1099511628211UL;
            }
        }

        private void OnMapLoad(APIShared.MissionLifecycleNotification args) => Safe(() => ProcessMapLoad(args));

        private void ProcessMapLoad(APIShared.MissionLifecycleNotification args)
        {
            if (args.IsBeforeInitialization)
            {
                ResetMap("OnLoadMap(Pre)");
                mapActive = true;
                currentMapIsSave = args.Context.IsSave;
            }
            else Safe(() =>
            {
                CaptureMapLoadBuildingIdentities("map-load.post-fallback");
            });
            Shared.DebugLogHelper.LogInfo(log, $"PREPLACED_MAP_LOAD: phase={args.Phase}, sequence={mapSequence}.");
        }

        private void OnMapStart(APIShared.MissionLifecycleNotification args) => Safe(() => ProcessMapStart(args));

        private void ProcessMapStart(APIShared.MissionLifecycleNotification args)
        {
            if (args.IsBeforeInitialization && args.Context.IsSave && args.Context.Mode.IsRealMultiplayer) currentMapIsSave = true;
            if (!args.IsBeforeInitialization)
            {
                CapturePreplacedBaseline();
                ApplyLegacyRuinTimerFix();
                bool hasBaselinePortal = preplacedBuildings.Values.Any(identity =>
                    IsPortalStructure((eStructs)identity.StructureType));
                if (hasBaselinePortal || mapLoadWallTiles.Count != 0)
                    ResolveWallAccessRoles();
                ResolveEconomyProfile();
                SynchronizePreplacedPortalOwnersAndActivate();
            }
            Shared.DebugLogHelper.LogInfo(log, $"PREPLACED_MAP_START: phase={args.Phase}, sequence={mapSequence}.");
        }

        private void SynchronizePreplacedPortalOwnersAndActivate()
        {
            if (!economyFixEnabled || currentMapIsSave || !economyProfileResolved ||
                !economyMapRelevant || nativeModuleBase == 0)
                return;
            try
            {
                SynchronizePreplacedPortalOwnersAndActivateCore();
            }
            catch (Exception ex)
            {
                DisableEconomyFix("portal-owner-synchronization", ex);
            }
        }

        private void SynchronizePreplacedPortalOwnersAndActivateCore()
        {
            int eligible = 0;
            int changed = 0;
            int alreadyCorrect = 0;
            int skipped = 0;
            foreach (PreplacedIdentity identity in preplacedBuildings.Values)
            {
                if (!IsPortalStructure((eStructs)identity.StructureType)) continue;
                bool identityMatches = TryCaptureBuilding(identity.BuildingId, out BuildingSnapshot building) &&
                    identity.MatchesStableRecord(building.Id, building.GlobalId, (int)building.Type);
                PathConnectionRecord* record = null;
                bool recordResolved = identityMatches &&
                    GamePathingManagerAPI.Instance.TryGetPathConnectionRecordByBuildingId(
                        identity.BuildingId, out record) && record != null;
                bool entryPclValid = recordResolved &&
                    TryGetPclByTileId(record->r_EntryTileId, out int entryPcl, "portal-owner-sync-entry") &&
                    entryPcl > 0;
                bool exitPclValid = recordResolved &&
                    TryGetPclByTileId(record->r_ExitTileId, out int exitPcl, "portal-owner-sync-exit") &&
                    exitPcl > 0;
                bool valid = economyAccessFix.IsPortalOwnerSynchronizationEligible(
                    currentMapIsSave,
                    preplacedBuildings.ContainsKey(identity.BuildingId),
                    identityMatches,
                    identityMatches && IsLiving(building),
                    recordResolved && record->r_IsActive != 0,
                    recordResolved && record->r_IsEnabledOrOpen != 0,
                    recordResolved && record->r_BuildingId == identity.BuildingId,
                    recordResolved && record->r_SubjectGlobalId != 0 &&
                        record->r_SubjectGlobalId == building.GlobalId,
                    entryPclValid,
                    exitPclValid,
                    identityMatches && IsValidOwner(building.OwnerId));
                if (!valid)
                {
                    skipped++;
                    continue;
                }

                eligible++;
                if (record->r_OwnerOrAccessPlayerId == building.OwnerId)
                {
                    alreadyCorrect++;
                    continue;
                }

                int previousOwner = record->r_OwnerOrAccessPlayerId;
                record->r_OwnerOrAccessPlayerId = building.OwnerId;
                if (record->r_OwnerOrAccessPlayerId != building.OwnerId)
                {
                    record->r_OwnerOrAccessPlayerId = previousOwner;
                    DisableEconomyFix("portal-owner-synchronization",
                        new InvalidOperationException(
                            "The validated portal owner field did not retain the synchronized value."));
                    return;
                }
                changed++;
            }

            if (eligible != 0 || skipped != 0)
                Shared.DebugLogHelper.LogInfo(log,
                    $"PREPLACED_PORTAL_OWNER_SYNC: sequence={mapSequence}; eligible={eligible}; " +
                    $"changed={changed}; alreadyCorrect={alreadyCorrect}; skipped={skipped}.");

            if (changed != 0)
                InvalidateEconomyOverlayCaches("preplaced-portal-owner-synchronized");

            if (lastAivState == 0)
            {
                if (eligible != 0)
                    Shared.DebugLogHelper.LogInfo(log,
                        $"PREPLACED_ECONOMY_FIX_PENDING: sequence={mapSequence}; trigger=map-start-owner-sync; " +
                        "reason=native-state-not-yet-captured.");
                return;
            }

            PlayerSession[] pending = players.Values
                .Where(session => session.EconomyFixState == EconomyFixActivationState.PendingPortal)
                .OrderBy(session => session.PlayerId)
                .ToArray();
            foreach (PlayerSession session in pending)
            {
                TryActivateOrRefreshEconomyFix(lastAivState, session.PlayerId, "map-start-owner-sync");
                if (!economyFixEnabled) return;
            }
        }

        private void OnMapUnload(APIShared.MissionLifecycleNotification args) => Safe(() => ProcessMapUnload(args));

        private void ProcessMapUnload(APIShared.MissionLifecycleNotification args)
        {
            mapActive = false;
        }

        private void OnBuildingDamage(BuildingTileTakeDamageEventArgs args)
        {
            try { ProcessBuildingDamage(args); }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, "PREPLACED_EVENT_ERROR: building-damage; " + ex);
            }
        }

        private void ProcessBuildingDamage(BuildingTileTakeDamageEventArgs args)
        {
            if (args.Phase == EventHookPhase.Pre)
            {
                // Once startup is complete, the ruins fix no longer needs combat
                // telemetry. Wall maps retain only enclosure-related observations.
                if (economyProfileResolved && !economyMapRelevant) return;
                DamageContext context = CaptureDamageContext(args);
                bool relevantEnclosure = context.Building.HasValue && IsCurrentPreplaced(context.Building.Value.Id) &&
                    IsEnclosureBuilding(context.Building.Value.Type);
                if (economyProfileResolved && !context.WallBefore.HasValue && !relevantEnclosure)
                {
                    pendingDamage.Push(DamageContext.Unmatched(args));
                    return;
                }
                if (!IsValidOwner(context.OwnerId))
                {
                    pendingDamage.Push(DamageContext.Unmatched(args));
                    return;
                }
                pendingDamage.Push(context);
                return;
            }

            if (pendingDamage.Count == 0) return;
            DamageContext completed = pendingDamage.Pop();
            int afterCounter = IsValidOwner(completed.OwnerId) ? ReadPlayerGlobal(completed.OwnerId, CrushedCounterRelativeOffset) : -1;
            if (completed.WallBefore.HasValue)
            {
                WallTileState afterWall = CaptureWallTileState(completed.WallBefore.Value.TileId,
                    completed.WallBefore.Value.X, completed.WallBefore.Value.Y);
                WallTileDelta wallDelta = new WallTileDelta(completed.WallBefore.Value, afterWall);
                if (wallDelta.WallLost)
                {
                    if (wallBaselines.TryGetValue(completed.OwnerId, out WallTileBaseline baseline))
                        baseline.LostWallTiles.Add(completed.WallBefore.Value.TileId);
                    InvalidateEconomyOverlayCaches("baseline-wall-tile-lost-by-damage");
                    dirtyBreachPlayers.Add(completed.OwnerId);
                }
            }
            if (CrushedTimerTransition.IsActivation(completed.DelayBefore, afterCounter))
                damageActivatedTimerOwners.Add(completed.OwnerId);
            if (completed.Building.HasValue)
            {
                BuildingSnapshot beforeBuilding = completed.Building.Value;
                bool wallOrPortal = IsWallStructure(beforeBuilding.Type) || IsPortalStructure(beforeBuilding.Type);
                bool mayHaveChangedRouting = wallOrPortal &&
                    (DamageObservationModel.IsLethalInput(beforeBuilding.CurrentHealth, args.Damage) ||
                     !TryCaptureBuilding(beforeBuilding.Id, out BuildingSnapshot remaining) ||
                     remaining.Alive != AliveState.IsAlive);
                if (mayHaveChangedRouting)
                {
                    InvalidateEconomyOverlayCaches("wall-or-portal-damage");
                    dirtyBreachPlayers.Add(completed.OwnerId);
                }
            }
        }

        private void ResolveEconomyProfile()
        {
            economyFixEligiblePlayers.Clear();
            economyOverlayCaches.Clear();
            foreach (int playerId in Enumerable.Range(1, MaxPlayablePlayerId).Where(IsAi))
            {
                PlayerSession session = Session(playerId);
                bool hasFriendlyPreplacedPortal = HasFriendlyPreplacedPortalBuilding(playerId);
                session.EconomyFixState = economyAccessFix.InitialState(currentMapIsSave,
                    session.WallAccessRole, hasFriendlyPreplacedPortal);
            }

            economyMapRelevant = wallBaselines.Count != 0 || players.Values.Any(session =>
                session.EconomyFixState != EconomyFixActivationState.None);
            economyProfileResolved = true;
            Shared.DebugLogHelper.LogInfo(log,
                $"PREPLACED_ECONOMY_PROFILE: sequence={mapSequence}; relevant={economyMapRelevant}; " +
                $"fixStates=[{string.Join(",", players.Values.Where(session =>
                    session.EconomyFixState != EconomyFixActivationState.None).OrderBy(session => session.PlayerId)
                    .Select(session => session.PlayerId + ":" + session.EconomyFixState))}].");
            foreach (PlayerSession session in players.Values.Where(value =>
                value.EconomyFixState == EconomyFixActivationState.PendingPortal ||
                value.EconomyFixState == EconomyFixActivationState.PendingBreach))
                EmitEconomyFixState("PENDING", session, "map-start-post",
                    session.EconomyFixState == EconomyFixActivationState.PendingPortal
                        ? "preplaced-friendly-portal-awaiting-native-route"
                        : "closed-baseline-wall-awaiting-confirmed-breach");
        }

        private static bool IsAllied(int firstPlayerId, int secondPlayerId)
        {
            if (!IsValidOwner(firstPlayerId) || !IsValidOwner(secondPlayerId)) return false;
            return firstPlayerId == secondPlayerId ||
                GamePlayerManagerAPI.Instance.IsPlayerAlliedTo(firstPlayerId, secondPlayerId);
        }

        private void OnBuildingBulldoze(BuildingBulldozeEventArgs args)
        {
            try { RecordRemoval("bulldoze", args.Phase, args.BuildingId); }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, "PREPLACED_EVENT_ERROR: building-bulldoze; " + ex);
            }
        }

        private void OnBuildingDelete(BuildingDeleteEventArgs args)
        {
            try { RecordRemoval("delete", args.Phase, args.BuildingId); }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, "PREPLACED_EVENT_ERROR: building-delete; " + ex);
            }
        }

        private void RecordRemoval(string kind, EventHookPhase phase, int buildingId)
        {
            if (phase == EventHookPhase.Pre)
            {
                if (TryCaptureBuilding(buildingId, out BuildingSnapshot building) &&
                    IsCurrentPreplaced(buildingId) && IsEnclosureBuilding(building.Type))
                    pendingRelevantRemovals[buildingId] = building.OwnerId;
                return;
            }
            if (!pendingRelevantRemovals.TryGetValue(buildingId, out int ownerId)) return;
            pendingRelevantRemovals.Remove(buildingId);
            InvalidateEconomyOverlayCaches(kind + "-preplaced-enclosure");
            dirtyBreachPlayers.Add(ownerId);
        }

        private void ResolveWallAccessRoles()
        {
            List<BuildingSnapshot> buildings = CaptureCurrentPreplacedBuildings();
            WallOwnerEncoding encoding = ResolveWallOwnerEncoding(buildings);
            for (int playerId = 1; playerId <= MaxPlayablePlayerId; playerId++)
            {
                if (!IsAi(playerId)) continue;
                WallTileBaseline baseline = CaptureWallBaseline(playerId, encoding, buildings);
                if (baseline != null) wallBaselines[playerId] = baseline;
                int wallCount = baseline?.ComponentTiles.Count ?? 0;
                BuildingSnapshot[] componentPortals = baseline == null ? Array.Empty<BuildingSnapshot>() : buildings.Where(building =>
                    building.OwnerId == playerId && IsCurrentPreplaced(building.Id) && IsPortalStructure(building.Type) &&
                    (building.Alive == AliveState.IsAlive || building.Alive == AliveState.NeedsInit) &&
                    BuildingFootprintOverlaps(building, baseline.ComponentBlockers)).ToArray();
                int portalCount = componentPortals.Length;
                WallAccessRole role = WallAccessRoleClassifier.Classify(wallCount, portalCount);
                PlayerSession session = Session(playerId);
                session.WallAccessRole = role;
            }
        }

        private WallOwnerEncoding ResolveWallOwnerEncoding(List<BuildingSnapshot> buildings)
        {
            int oneBasedMatches = 0;
            int zeroBasedMatches = 0;
            GameTileManagerAPI api = GameTileManagerAPI.Instance;
            var tiles = api.TileManager;
            foreach (BuildingSnapshot building in buildings.Where(value => IsCurrentPreplaced(value.Id) &&
                IsLiving(value) && IsPortalStructure(value.Type)))
            {
                for (int x = building.TileX; x <= building.EndX; x++)
                    for (int y = building.TileY; y <= building.EndY; y++)
                    {
                        if (!api.IsTileInsideMapBounds(x, y)) continue;
                        int tileId = api.GetTileId(x, y);
                        if ((tiles.LogicGrid[tileId] & (int)TilePropertyFlag.IsWall) == 0) continue;
                        byte raw = tiles.WallOwnerGrid[tileId];
                        if (raw == building.OwnerId) oneBasedMatches++;
                        if (raw + 1 == building.OwnerId) zeroBasedMatches++;
                    }
            }
            return WallOwnerEncodingResolver.Resolve(oneBasedMatches, zeroBasedMatches);
        }

        private static bool BuildingFootprintOverlaps(BuildingSnapshot building, HashSet<int> tileIds)
        {
            GameTileManagerAPI api = GameTileManagerAPI.Instance;
            for (int x = building.TileX; x <= building.EndX; x++)
                for (int y = building.TileY; y <= building.EndY; y++)
                    if (api.IsTileInsideMapBounds(x, y) && tileIds.Contains(api.GetTileId(x, y)))
                        return true;
            return false;
        }

        private WallTileBaseline CaptureWallBaseline(int playerId, WallOwnerEncoding encoding,
            List<BuildingSnapshot> buildings)
        {
            if (encoding == WallOwnerEncoding.Unresolved) return null;
            if (!TryGetKeepPosition(playerId, out int keepX, out int keepY)) return null;
            GameTileManagerAPI api = GameTileManagerAPI.Instance;
            var manager = api.TileManager;
            var all = new Dictionary<int, WallTileState>();
            for (int x = 0; x < NativeTileGridWidth; x++)
                for (int y = 0; y < NativeTileGridWidth; y++)
                {
                    if (!api.IsTileInsideMapBounds(x, y)) continue;
                    int tileId = api.GetTileId(x, y);
                    if (!mapLoadWallTiles.Contains(tileId)) continue;
                    if ((manager.LogicGrid[tileId] & (int)TilePropertyFlag.IsWall) == 0) continue;
                    byte rawOwner = manager.WallOwnerGrid[tileId];
                    if (WallOwnerEncodingResolver.Decode(rawOwner, encoding) != playerId) continue;
                    all[tileId] = CaptureWallTileState(tileId, x, y);
                }
            if (all.Count == 0) return null;

            // Keeps expose wall flags themselves. They are not part of a surrounding
            // player-built enclosure and previously caused the 25-tile false component.
            foreach (BuildingSnapshot building in buildings.Where(value => value.OwnerId == playerId &&
                IsLiving(value) && value.TileX <= keepX && keepX <= value.EndX &&
                value.TileY <= keepY && keepY <= value.EndY))
                for (int x = building.TileX; x <= building.EndX; x++)
                    for (int y = building.TileY; y <= building.EndY; y++)
                        if (api.IsTileInsideMapBounds(x, y)) all.Remove(api.GetTileId(x, y));
            if (all.Count == 0) return null;

            int keepTileId = api.GetTileId(keepX, keepY);
            var blockers = new HashSet<int>(all.Keys);
            foreach (BuildingSnapshot enclosure in buildings.Where(value => value.OwnerId == playerId &&
                IsCurrentPreplaced(value.Id) && IsLiving(value) && IsEnclosureBuilding(value.Type)))
                for (int x = enclosure.TileX; x <= enclosure.EndX; x++)
                    for (int y = enclosure.TileY; y <= enclosure.EndY; y++)
                        if (api.IsTileInsideMapBounds(x, y)) blockers.Add(api.GetTileId(x, y));

            HashSet<int> interior = FloodPassable(new[] { keepTileId }, blockers, api);
            HashSet<int> exterior = FloodPassable(EnumerateMapBoundaryTiles(blockers, api), blockers, api);
            HashSet<int> componentBlockers = SelectEnclosureBlockerComponent(blockers, all,
                interior, exterior, api);
            var component = new HashSet<int>(componentBlockers.Where(all.ContainsKey));
            var anchors = new List<WallAnchorPair>();
            foreach (int wallTileId in component)
            {
                WallTileState wall = all[wallTileId];
                int insideTile = FindNearestRegionTile(wall.X, wall.Y, interior, api);
                int outsideTile = FindNearestRegionTile(wall.X, wall.Y, exterior, api);
                if (insideTile < 0 || outsideTile < 0) continue;
                int insidePcl = TryGetPclByTileId(insideTile, out int capturedInside, "wall-anchor-inside") ? capturedInside : 0;
                int outsidePcl = TryGetPclByTileId(outsideTile, out int capturedOutside, "wall-anchor-outside") ? capturedOutside : 0;
                if (insidePcl <= 0 || outsidePcl <= 0 || insidePcl == outsidePcl) continue;
                anchors.Add(new WallAnchorPair(wallTileId, insideTile, outsideTile, insidePcl, outsidePcl));
            }
            AddPclAdjacencyAnchors(component, anchors, keepTileId, api);
            return new WallTileBaseline(playerId, keepX, keepY, all, component,
                componentBlockers, !interior.Overlaps(exterior), anchors);
        }

        private void AddPclAdjacencyAnchors(HashSet<int> component, List<WallAnchorPair> anchors,
            int keepTileId, GameTileManagerAPI api)
        {
            if (!TryGetPclByTileId(keepTileId, out int keepPcl, "wall-anchor-keep")) keepPcl = 0;
            var known = new HashSet<string>(anchors.Select(value => value.InsideTileId + "/" + value.OutsideTileId));
            int before = anchors.Count;
            AddPclAdjacencyAnchors(component, anchors, known, api, keepPcl);
            if (anchors.Count == before && keepPcl > 0)
                AddPclAdjacencyAnchors(component, anchors, known, api, 0);
        }

        private void AddPclAdjacencyAnchors(HashSet<int> component, List<WallAnchorPair> anchors,
            HashSet<string> known, GameTileManagerAPI api, int requiredInsidePcl)
        {
            foreach (int wallTileId in component)
            {
                var vector = api.GetTileVectorFromId(wallTileId);
                var adjacent = new List<(int TileId, int Pcl)>();
                foreach (int tileId in GetAdjacentTileIds((int)vector.X, (int)vector.Y, api, true))
                    if (!component.Contains(tileId) && TryGetPclByTileId(tileId, out int pcl, "wall-anchor-adjacent") && pcl > 0)
                        adjacent.Add((tileId, pcl));
                foreach (var inside in adjacent)
                    foreach (var outside in adjacent)
                    {
                        if (inside.TileId == outside.TileId || inside.Pcl == outside.Pcl) continue;
                        if (requiredInsidePcl > 0 && inside.Pcl != requiredInsidePcl) continue;
                        string key = inside.TileId + "/" + outside.TileId;
                        if (!known.Add(key)) continue;
                        anchors.Add(new WallAnchorPair(wallTileId, inside.TileId, outside.TileId,
                            inside.Pcl, outside.Pcl));
                    }
            }
        }

        private static HashSet<int> SelectEnclosureBlockerComponent(HashSet<int> blockers,
            Dictionary<int, WallTileState> walls, HashSet<int> interior, HashSet<int> exterior,
            GameTileManagerAPI api)
        {
            var remaining = new HashSet<int>(blockers);
            HashSet<int> best = new HashSet<int>();
            int bestWallCount = 0;
            var candidateComponents = new List<HashSet<int>>();
            while (remaining.Count != 0)
            {
                int seed = remaining.First();
                remaining.Remove(seed);
                var component = new HashSet<int> { seed };
                var queue = new Queue<int>();
                queue.Enqueue(seed);
                bool touchesInterior = false;
                bool touchesExterior = false;
                while (queue.Count != 0)
                {
                    int tileId = queue.Dequeue();
                    var vector = api.GetTileVectorFromId(tileId);
                    foreach (int neighbor in GetOrthogonalTileIds((int)vector.X, (int)vector.Y, api))
                    {
                        if (interior.Contains(neighbor)) touchesInterior = true;
                        if (exterior.Contains(neighbor)) touchesExterior = true;
                    }
                    foreach (int neighbor in GetAdjacentTileIds((int)vector.X, (int)vector.Y, api, true))
                        if (remaining.Remove(neighbor) && blockers.Contains(neighbor))
                        { component.Add(neighbor); queue.Enqueue(neighbor); }
                }
                int wallCount = component.Count(walls.ContainsKey);
                if (wallCount != 0) candidateComponents.Add(component);
                if (touchesInterior && touchesExterior && wallCount > bestWallCount)
                { bestWallCount = wallCount; best = component; }
            }
            if (best.Count == 0 && candidateComponents.Count != 0)
                best = candidateComponents.OrderByDescending(value => value.Count(walls.ContainsKey)).First();
            return best;
        }

        private static HashSet<int> FloodPassable(IEnumerable<int> seeds, HashSet<int> blockers,
            GameTileManagerAPI api)
        {
            var reached = new HashSet<int>();
            var queue = new Queue<int>();
            foreach (int seed in seeds)
                if (!blockers.Contains(seed) && reached.Add(seed)) queue.Enqueue(seed);
            while (queue.Count != 0)
            {
                int tileId = queue.Dequeue();
                var vector = api.GetTileVectorFromId(tileId);
                foreach (int neighbor in GetOrthogonalTileIds((int)vector.X, (int)vector.Y, api))
                    if (!blockers.Contains(neighbor) && reached.Add(neighbor)) queue.Enqueue(neighbor);
            }
            return reached;
        }

        private static IEnumerable<int> EnumerateMapBoundaryTiles(HashSet<int> blockers,
            GameTileManagerAPI api)
        {
            for (int x = 0; x < NativeTileGridWidth; x++)
                for (int y = 0; y < NativeTileGridWidth; y++)
                {
                    if (!api.IsTileInsideMapBounds(x, y)) continue;
                    int tileId = api.GetTileId(x, y);
                    if (blockers.Contains(tileId)) continue;
                    if (!api.IsTileInsideMapBounds(x - 1, y) || !api.IsTileInsideMapBounds(x + 1, y) ||
                        !api.IsTileInsideMapBounds(x, y - 1) || !api.IsTileInsideMapBounds(x, y + 1))
                        yield return tileId;
                }
        }

        private static int FindNearestRegionTile(int x, int y, HashSet<int> region,
            GameTileManagerAPI api)
        {
            for (int distance = 1; distance < NativeTileGridWidth; distance++)
            {
                for (int dx = -distance; dx <= distance; dx++)
                {
                    int dy = distance - Math.Abs(dx);
                    if (api.IsTileInsideMapBounds(x + dx, y + dy))
                    {
                        int tileId = api.GetTileId(x + dx, y + dy);
                        if (region.Contains(tileId)) return tileId;
                    }
                    if (dy != 0 && api.IsTileInsideMapBounds(x + dx, y - dy))
                    {
                        int tileId = api.GetTileId(x + dx, y - dy);
                        if (region.Contains(tileId)) return tileId;
                    }
                }
            }
            return -1;
        }

        private static IEnumerable<int> GetAdjacentTileIds(int x, int y, GameTileManagerAPI api,
            bool includeDiagonals)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    if (!includeDiagonals && dx != 0 && dy != 0) continue;
                    if (api.IsTileInsideMapBounds(x + dx, y + dy)) yield return api.GetTileId(x + dx, y + dy);
                }
        }

        private static IEnumerable<int> GetOrthogonalTileIds(int x, int y, GameTileManagerAPI api)
        {
            if (api.IsTileInsideMapBounds(x - 1, y)) yield return api.GetTileId(x - 1, y);
            if (api.IsTileInsideMapBounds(x + 1, y)) yield return api.GetTileId(x + 1, y);
            if (api.IsTileInsideMapBounds(x, y - 1)) yield return api.GetTileId(x, y - 1);
            if (api.IsTileInsideMapBounds(x, y + 1)) yield return api.GetTileId(x, y + 1);
        }

        private WallTileState CaptureWallTileState(int tileId, int x, int y)
        {
            var manager = GameTileManagerAPI.Instance.TileManager;
            return new WallTileState(tileId, x, y, manager.LogicGrid[tileId], manager.WallOwnerGrid[tileId],
                manager.DamageGrid[tileId], manager.StructureWasGrid[tileId], manager.GatePathGrid[tileId],
                manager.HeightGrid[tileId], manager.DefaultHeightGrid[tileId],
                TryGetPclByTileId(tileId, out int pcl, "wall-tile") ? pcl : 0);
        }

        private static bool TryGetKeepPosition(int playerId, out int x, out int y)
        {
            x = 0; y = 0;
            if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources) || resources == null)
                return false;
            x = checked((int)resources->r_KeepTilePositionX);
            y = checked((int)resources->r_KeepTilePositionY);
            return GameTileManagerAPI.Instance.IsTileInsideMapBounds(x, y);
        }

        private static bool IsLiving(BuildingSnapshot building) =>
            building.Alive == AliveState.IsAlive || building.Alive == AliveState.NeedsInit;

        private void ResetMap(string reason)
        {
            players.Clear();
            mapSequence++;
            activeEconomyOverlayScopes?.Clear();
            pendingDamage.Clear();
            mapLoadBuildingIdentities.Clear();
            mapLoadWallTiles.Clear();
            preAivBaselineCaptured = false;
            preplacedBuildings.Clear();
            wallBaselines.Clear();
            mapActive = false;
            currentMapIsSave = false;
            lastLegacyCopyMapVersion = -1;
            lastLegacyCopySourceBefore = null;
            lastLegacyCopyDestinationBefore = null;
            lastLegacyCopySource = null;
            lastLegacyCopyDestination = null;
            legacyCopyBuildings.Clear();
            damageActivatedTimerOwners.Clear();
            dirtyBreachPlayers.Clear();
            pendingRelevantRemovals.Clear();
            overlayScratchInUse = false;
            emittedInvalidPclAccesses.Clear();
            economyFixEligiblePlayers.Clear();
            economyOverlayCaches.Clear();
            economyProfileResolved = false;
            economyMapRelevant = false;
            reconcilingEconomyAvailability = false;
            resolvingEconomyOverlayRoutes = false;
            economyTopologyRevision = 0;
            lastAivState = 0;
            Shared.DebugLogHelper.LogInfo(log, $"PREPLACED_SESSION_RESET: reason={reason}, sequence={mapSequence}.");
        }

        private PlayerSession Session(int playerId)
        {
            if (!IsAi(playerId))
                throw new InvalidOperationException("AI session requested for unresolved player " + playerId + ".");
            if (!players.TryGetValue(playerId, out PlayerSession session))
            {
                session = new PlayerSession(playerId);
                players.Add(playerId, session);
            }
            return session;
        }

        private int ReadPlayerGlobal(int playerId, int relativeOffset)
        {
            if (activeLayoutIndexBase == 0 || playerId < 0 || playerId > MaxPlayablePlayerId) return 0;
            return *(int*)(activeLayoutIndexBase + (ulong)(playerId * PlayerRuntimeStateStride + relativeOffset));
        }

        private short ReadPlayerInt16(int playerId, int relativeOffset, int elementIndex)
        {
            if (activeLayoutIndexBase == 0 || playerId < 0 || playerId > MaxPlayablePlayerId || elementIndex < 0)
                return 0;
            return *(short*)(activeLayoutIndexBase + (ulong)(playerId * PlayerRuntimeStateStride + relativeOffset + elementIndex * sizeof(short)));
        }

        private List<BuildingSnapshot> CaptureRawBuildings()
        {
            List<BuildingSnapshot> result = new List<BuildingSnapshot>();
            Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            for (int spanIndex = 0; spanIndex < buildings.Length; spanIndex++)
            {
                ref GameBuilding building = ref buildings[spanIndex];
                if (HasAnyNonZeroByte(ref building))
                    result.Add(Snapshot(spanIndex + 1, ref building));
            }
            return result;
        }

        private static List<BuildingSnapshot> CaptureDestroyedTowers()
        {
            var result = new List<BuildingSnapshot>(8);
            Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            for (int spanIndex = 0; spanIndex < buildings.Length; spanIndex++)
            {
                ref GameBuilding building = ref buildings[spanIndex];
                if (IsDestroyedTower(building.r_BuildingType))
                    result.Add(Snapshot(spanIndex + 1, ref building));
            }
            return result;
        }

        private static bool HasAnyNonZeroByte(ref GameBuilding building)
        {
            fixed (GameBuilding* buildingPointer = &building)
            {
                byte* bytes = (byte*)buildingPointer;
                for (int offset = 0; offset < sizeof(GameBuilding); offset++)
                    if (bytes[offset] != 0) return true;
            }
            return false;
        }

        private void CapturePreplacedBaseline()
        {
            preplacedBuildings.Clear();
            foreach (PreplacedIdentity loadedIdentity in mapLoadBuildingIdentities.Values)
            {
                if (!TryCaptureBuilding(loadedIdentity.BuildingId, out BuildingSnapshot building) ||
                    !loadedIdentity.MatchesStableRecord(building.Id, building.GlobalId, (int)building.Type))
                    continue;
                // Use the current owner after map initialization, but only for a stable
                // record captured before the first AllocateSpec original call. AIV-created
                // gates are therefore never promoted to preplaced portals.
                preplacedBuildings[building.Id] = building.Identity;
            }
        }

        private List<BuildingSnapshot> CaptureCurrentPreplacedBuildings()
        {
            var result = new List<BuildingSnapshot>(preplacedBuildings.Count);
            foreach (PreplacedIdentity identity in preplacedBuildings.Values)
            {
                if (TryCaptureBuilding(identity.BuildingId, out BuildingSnapshot building) &&
                    identity.MatchesStableRecord(building.Id, building.GlobalId, (int)building.Type))
                    result.Add(building);
            }
            return result;
        }

        private void CaptureMapLoadBuildingIdentities(string source)
        {
            if (preAivBaselineCaptured) return;
            mapLoadBuildingIdentities.Clear();
            mapLoadWallTiles.Clear();
            foreach (BuildingSnapshot building in CaptureRawBuildings())
                if (building.GlobalId != 0)
                    mapLoadBuildingIdentities[building.Id] = building.Identity;
            GameTileManagerAPI api = GameTileManagerAPI.Instance;
            var tiles = api.TileManager;
            for (int x = 0; x < NativeTileGridWidth; x++)
                for (int y = 0; y < NativeTileGridWidth; y++)
                {
                    if (!api.IsTileInsideMapBounds(x, y)) continue;
                    int tileId = api.GetTileId(x, y);
                    if ((tiles.LogicGrid[tileId] & (int)TilePropertyFlag.IsWall) != 0)
                        mapLoadWallTiles.Add(tileId);
                }
            preAivBaselineCaptured = true;
            Shared.DebugLogHelper.LogInfo(log,
                $"PREPLACED_PRE_AIV_BASELINE: sequence={mapSequence}; source={source}; " +
                $"stableRecords={mapLoadBuildingIdentities.Count}; " +
                $"wallTiles={mapLoadWallTiles.Count}.");
        }

        private bool IsCurrentPreplaced(int buildingId)
        {
            return preplacedBuildings.TryGetValue(buildingId, out PreplacedIdentity identity) &&
                GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building) && building != null &&
                identity.Matches(buildingId, building->r_GlobalId, building->r_PlayerIdOwner, (int)building->r_BuildingType);
        }

        private bool TryGetKeepPcl(int playerId, out int pcl)
        {
            pcl = 0;
            return GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources) &&
                resources != null && TryGetPclByTileId(checked((int)resources->r_KeepTileId), out pcl, "keep");
        }

        private bool TryGetPclAt(int x, int y, out int pcl)
        {
            pcl = 0;
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            return tiles.IsTileInsideMapBounds(x, y) &&
                TryGetPclByTileId(tiles.GetTileId(x, y), out pcl, "coordinate");
        }

        private bool TryGetPclByTileId(int tileId, out int pcl, string source = "tile-id")
        {
            pcl = 0;
            if (nativePclGrid == null) return false;
            if ((uint)tileId >= NativePclEntryCount)
            {
                RecordInvalidPclAccess(tileId, source);
                return false;
            }
            pcl = nativePclGrid[tileId];
            return pcl > 0;
        }

        private void RecordInvalidPclAccess(int tileId, string source)
        {
            string signature = source + "/" + tileId;
            if (emittedInvalidPclAccesses.Add(signature))
                Shared.DebugLogHelper.LogWarning(log,
                    $"PREPLACED_INVALID_PCL_TILE_ACCESS: source={source}; tileId={tileId}; valid=0..{NativePclEntryCount - 1}.");
        }

        private bool TryCaptureBuilding(int buildingId, out BuildingSnapshot snapshot)
        {
            snapshot = default;
            if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building)) return false;
            snapshot = Snapshot(buildingId, ref *building);
            return true;
        }

        private static BuildingSnapshot Snapshot(int buildingId, ref GameBuilding building) =>
            new BuildingSnapshot(buildingId, building.r_GlobalId, building.r_PlayerIdOwner, building.r_BuildingType,
                building.r_AliveState, building.r_TilePositionXBegin, building.r_TilePositionYBegin,
                building.r_AccessTilePositionX, building.r_AccessTilePositionY,
                building.r_CurrentHealth, building.r_GatehouseId);

        private DamageContext CaptureDamageContext(BuildingTileTakeDamageEventArgs args)
        {
            try
            {
                int buildingId = GameTileManagerAPI.Instance.GetTileBuildingId(args.TileId);
                if (buildingId > 0 && TryCaptureBuilding(buildingId, out BuildingSnapshot building))
                    return new DamageContext(building, ReadPlayerGlobal(building.OwnerId, CrushedCounterRelativeOffset),
                        IsCurrentPreplaced(buildingId));
                foreach (WallTileBaseline baseline in wallBaselines.Values)
                {
                    if (!baseline.Tiles.ContainsKey(args.TileId)) continue;
                    WallTileState wall = CaptureWallTileState(args.TileId, args.TileX, args.TileY);
                    return DamageContext.ForBaselineWall(wall, baseline.PlayerId,
                        ReadPlayerGlobal(baseline.PlayerId, CrushedCounterRelativeOffset));
                }
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log,
                    "PREPLACED_EVENT_ERROR: damage-context; " + ex);
            }
            return DamageContext.Unmatched(args);
        }

        private bool IsAi(int playerId)
        {
            try { return playerId >= 1 && playerId <= MaxPlayablePlayerId && GamePlayerManagerAPI.Instance.IsAIPlayer(playerId); }
            catch { return false; }
        }

        private static bool IsValidOwner(int playerId) => playerId >= 1 && playerId <= MaxPlayablePlayerId;

        private void Safe(Action action)
        {
            try { action(); }
            catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "PREPLACED_CALLBACK_ERROR: Vanilla result was preserved. " + ex); }
        }

        private readonly struct NativeDefinition
        {
            public NativeDefinition(string name, string pattern, int rva)
            {
                Name = name;
                Pattern = pattern;
                Rva = rva;
            }

            public string Name { get; }
            public string Pattern { get; }
            public int Rva { get; }
        }

        private sealed class PlayerSession
        {
            public PlayerSession(int playerId) => PlayerId = playerId;

            public int PlayerId { get; }
            public WallAccessRole WallAccessRole { get; set; }
            public EconomyFixActivationState EconomyFixState { get; set; }
            public int EconomyFixActivationEpoch { get; set; }
            public int EconomyFixValidatedRevision { get; set; } = -1;
            private EconomyFixActivationState lastWaitState;
            private int lastWaitRevision = int.MinValue;
            private bool lastWaitWasCensus;
            public bool ConfirmedWallBreach { get; set; }

            public bool MatchesWaitState(EconomyFixActivationState state, int revision, bool census) =>
                lastWaitRevision == revision && lastWaitState == state && lastWaitWasCensus == census;

            public void SetWaitState(EconomyFixActivationState state, int revision, bool census)
            {
                lastWaitState = state;
                lastWaitRevision = revision;
                lastWaitWasCensus = census;
            }

            public void ClearWaitState() => lastWaitRevision = int.MinValue;
        }

        private sealed class EconomyOverlayCache
        {
            public EconomyOverlayCache(int revision, ulong accessSignature, int startPcl,
                int[] reachablePcls, int presentPclCount, byte[] projected)
            {
                Revision = revision;
                AccessSignature = accessSignature;
                StartPcl = startPcl;
                ReachablePcls = reachablePcls ?? Array.Empty<int>();
                PresentPclCount = presentPclCount;
                Projected = projected ?? throw new ArgumentNullException(nameof(projected));
            }

            public int Revision { get; }
            public ulong AccessSignature { get; }
            public int StartPcl { get; }
            public int[] ReachablePcls { get; }
            public int PresentPclCount { get; }
            public byte[] Projected { get; }
        }

        private readonly struct FriendlyPortalEdge
        {
            public FriendlyPortalEdge(int firstPcl, int secondPcl)
            {
                FirstPcl = firstPcl;
                SecondPcl = secondPcl;
            }

            public int FirstPcl { get; }
            public int SecondPcl { get; }

            public bool Connects(int first, int second) =>
                FirstPcl == first && SecondPcl == second ||
                FirstPcl == second && SecondPcl == first;
        }

        private sealed class EconomyGridOverlayScope
        {
            public void Reset(ulong state, int playerId, string helper, int keepPcl,
                int[] reachablePcls, int presentPclCount, int changedCells)
            {
                State = state;
                PlayerId = playerId;
                Helper = helper ?? "unknown";
                KeepPcl = keepPcl;
                ReachablePcls = reachablePcls ?? Array.Empty<int>();
                PresentPclCount = presentPclCount;
                ChangedCells = changedCells;
                WoodScoreFloorApplied = false;
            }

            public ulong State { get; private set; }
            public int PlayerId { get; private set; }
            public string Helper { get; private set; }
            public int KeepPcl { get; private set; }
            public int[] ReachablePcls { get; private set; }
            public int PresentPclCount { get; private set; }
            public int ChangedCells { get; private set; }
            public bool WoodScoreFloorApplied { get; set; }
        }

        private sealed class WallTileBaseline
        {
            public WallTileBaseline(int playerId, int keepX, int keepY,
                Dictionary<int, WallTileState> tiles, HashSet<int> componentTiles,
                HashSet<int> componentBlockers, bool geometryClosed, List<WallAnchorPair> anchors)
            {
                PlayerId = playerId;
                KeepX = keepX;
                KeepY = keepY;
                Tiles = tiles;
                ComponentTiles = componentTiles;
                ComponentBlockers = componentBlockers;
                GeometryClosed = geometryClosed;
                Anchors = anchors;
            }

            public int PlayerId { get; }
            public int KeepX { get; }
            public int KeepY { get; }
            public Dictionary<int, WallTileState> Tiles { get; }
            public HashSet<int> LostWallTiles { get; } = new HashSet<int>();
            public HashSet<int> ComponentTiles { get; }
            public HashSet<int> ComponentBlockers { get; }
            public bool GeometryClosed { get; }
            public List<WallAnchorPair> Anchors { get; }
        }

        private readonly struct WallTileState
        {
            public WallTileState(int tileId, int x, int y, int logic, byte rawOwner, byte damage,
                byte structureWas, byte gatePath, byte height, byte defaultHeight, int pcl)
            {
                TileId = tileId;
                X = x;
                Y = y;
                Logic = logic;
                RawOwner = rawOwner;
                Damage = damage;
                StructureWas = structureWas;
                GatePath = gatePath;
                Height = height;
                DefaultHeight = defaultHeight;
                Pcl = pcl;
            }

            public int TileId { get; }
            public int X { get; }
            public int Y { get; }
            public int Logic { get; }
            public byte RawOwner { get; }
            public byte Damage { get; }
            public byte StructureWas { get; }
            public byte GatePath { get; }
            public byte Height { get; }
            public byte DefaultHeight { get; }
            public int Pcl { get; }
            public bool IsWall => (Logic & (int)TilePropertyFlag.IsWall) != 0;
        }

        private sealed class WallAnchorPair
        {
            public WallAnchorPair(int wallTileId, int insideTileId, int outsideTileId,
                int oldInsidePcl, int oldOutsidePcl)
            {
                WallTileId = wallTileId;
                InsideTileId = insideTileId;
                OutsideTileId = outsideTileId;
                OldInsidePcl = oldInsidePcl;
                OldOutsidePcl = oldOutsidePcl;
            }

            public int WallTileId { get; }
            public int InsideTileId { get; }
            public int OutsideTileId { get; }
            public int OldInsidePcl { get; }
            public int OldOutsidePcl { get; }
        }

        private readonly struct WallTileDelta
        {
            public WallTileDelta(WallTileState before, WallTileState after)
            {
                Before = before;
                After = after;
            }

            public WallTileState Before { get; }
            public WallTileState After { get; }
            public bool WallLost => Before.IsWall && !After.IsWall;
        }

        private readonly struct BuildingSnapshot
        {
            public BuildingSnapshot(int id, uint globalId, int ownerId, eStructs type,
                AliveState alive, int tileX, int tileY, int endX, int endY,
                int currentHealth, int gatehouseId)
            {
                Id = id;
                GlobalId = globalId;
                OwnerId = ownerId;
                Type = type;
                Alive = alive;
                TileX = tileX;
                TileY = tileY;
                EndX = endX;
                EndY = endY;
                CurrentHealth = currentHealth;
                GatehouseId = gatehouseId;
            }

            public int Id { get; }
            public uint GlobalId { get; }
            public int OwnerId { get; }
            public eStructs Type { get; }
            public AliveState Alive { get; }
            public int TileX { get; }
            public int TileY { get; }
            public int EndX { get; }
            public int EndY { get; }
            public int CurrentHealth { get; }
            public int GatehouseId { get; }
            public PreplacedIdentity Identity =>
                new PreplacedIdentity(Id, GlobalId, OwnerId, (int)Type);
        }

        private sealed class DamageContext
        {
            private DamageContext(BuildingSnapshot? building, WallTileState? wallBefore,
                int wallOwnerId, int delayBefore)
            {
                Building = building;
                WallBefore = wallBefore;
                WallOwnerId = wallOwnerId;
                DelayBefore = delayBefore;
            }

            public BuildingSnapshot? Building { get; }
            public WallTileState? WallBefore { get; }
            public int OwnerId => Building.HasValue ? Building.Value.OwnerId : WallOwnerId;
            public int WallOwnerId { get; }
            public int DelayBefore { get; }

            public static DamageContext Unmatched(BuildingTileTakeDamageEventArgs args) =>
                new DamageContext(null, null, 0, -1);

            public static DamageContext ForBaselineWall(
                WallTileState wall, int ownerId, int delayBefore) =>
                new DamageContext(null, wall, ownerId, delayBefore);

            public DamageContext(BuildingSnapshot building, int delayBefore, bool wasPreplaced) :
                this(building, null, 0, delayBefore)
            {
            }
        }

        private static bool IsPortalStructure(eStructs type) =>
            type == eStructs.STRUCT_GATE_MAIN || type == eStructs.STRUCT_GATE_INNER ||
            type == eStructs.STRUCT_GATE_WOOD || type == eStructs.STRUCT_GATE_POSTERN ||
            type == eStructs.STRUCT_DRAWBRIDGE || type == eStructs.STRUCT_GATEHOUSE;

        private static bool IsWallStructure(eStructs type) =>
            type == eStructs.STRUCT_WOOD_WALL || type == eStructs.STRUCT_STONE_WALL ||
            type == eStructs.STRUCT_CRENAL_WALL || type == eStructs.STRUCT_WAS_WALL;

        private static bool IsDestroyedTower(eStructs type) =>
            type == eStructs.STRUCT_TOWER1_DESTROYED ||
            type == eStructs.STRUCT_TOWER2_DESTROYED ||
            type == eStructs.STRUCT_TOWER3_DESTROYED ||
            type == eStructs.STRUCT_TOWER4_DESTROYED ||
            type == eStructs.STRUCT_TOWER5_DESTROYED;

        private static bool IsEnclosureBuilding(eStructs type) =>
            IsPortalStructure(type) || type == eStructs.STRUCT_TOWER ||
            type == eStructs.STRUCT_TOWER1 || type == eStructs.STRUCT_TOWER2 ||
            type == eStructs.STRUCT_TOWER3 || type == eStructs.STRUCT_TOWER4 ||
            type == eStructs.STRUCT_TOWER5;
    }
}
