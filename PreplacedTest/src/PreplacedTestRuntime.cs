using BepInEx.Logging;
using R3;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
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
        private const int MaxAivSpecIndex = 8;
        private const int AivSpecStride = 0x6D98;
        private const int PlayerRuntimeStateStride = 0x583C;
        private const int PreparedLayoutFrameCount = 0x922;
        private const int PreparedEntrySize = 0x0C;
        private const int PreparedEntryBaseOffset = 0x38;
        private const int PlayerIdOffset = 0x04;
        private const int OrientationOffset = 0x0C;
        private const int CandidateIdOffset = 0x10;
        private const int PlacementStateOffset = 0x14;
        private const int CurrentStepGoalOffset = 0x18;
        private const int BuildCounterOffset = 0x1C;
        private const int BuildRateOffset = 0x20;
        private const int HighestPreparedFrameOffset = 0x24;
        private const int OriginXOffset = 0x28;
        private const int OriginYOffset = 0x2C;
        private const int KeepXOffset = 0x30;
        private const int KeepYOffset = 0x34;
        private const int ActiveAicRelativeOffset = 0x04;
        private const int CrushedCounterRelativeOffset = 0x7E4;
        private const int EconomyPhaseRelativeOffset = 0x1564;
        private const int PauseIndexRelativeOffset = 0x1568;
        private const int PauseCounterRelativeOffset = 0x156C;
        private const int PauseTableRelativeOffset = 0x1570;
        private const int PauseConfiguredRelativeOffset = 0x1598;
        private const int AivGridSize = 100;
        private const int LogPayloadLength = 1600;

        private const string AllocateSpecPattern =
            "48 89 74 24 10 57 48 83 EC 20 BF 01 00 00 00 48 8D 81 9C 6D 00 00";
        private const string SetPlacementPattern =
            "40 53 48 83 EC 30 48 63 C2 45 8B D1 48 69 D8 98 6D 00 00";
        private const string SelectBestFitPattern =
            "44 88 44 24 18 89 54 24 10 55 56 41 54 41 55 41 56 41 57 48 83 EC 58";
        private const string TestSpecificCandidatePattern =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 48 89 7C 24 20 41 56 48 83 EC 20 41 8B F0 48 63 EA";
        private const string LoadCandidatePattern =
            "40 53 56 57 41 55 48 83 EC 38 8B 05 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? 41 8B D8 48 63 FA 85 C0";
        private const string ApplyRotationPattern =
            "85 D2 0F 84 ?? ?? ?? ?? 53 48 83 EC 20 48 89 74 24 30 48 8B D9 48 89 7C 24 38 83 FA 06";
        private const string EvaluateCandidateFitPattern =
            "89 54 24 10 53 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 48 45 33 C9 48 8D 81 44 98 1B 00";
        private const string PrepareLayoutPattern =
            "44 89 44 24 18 53 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 68";
        private const string SchedulerPattern =
            "48 89 6C 24 10 48 89 74 24 18 57 41 54 41 55 41 56 41 57 48 83 EC 30 48 63 F2 48 8D 05 ?? ?? ?? ??";
        private const string ExecuteBuildStepPattern =
            "40 53 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 78 4C 63 F2";
        private const string AlternativeExecutionPattern =
            "44 89 44 24 18 89 54 24 10 48 89 4C 24 08 53 55 56 57 41 54 41 55 41 56 41 57 48 81 EC C8 00 00 00";
        private const string PlacementHelperPattern =
            "44 89 4C 24 20 44 89 44 24 18 89 54 24 10 53 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 48 " +
            "44 8B BC 24 B8 00 00 00 45 8B E1 41 8B E8 89 54 24 20 45 8B C4 8B D5";
        private const string ValidatorPattern =
            "40 53 55 56 57 41 56 48 83 EC 40 33 C0 49 63 E8 83 BC 24 90 00 00 00 02";
        private const string ResourceGatePattern =
            "48 89 5C 24 20 44 89 44 24 18 89 54 24 10 48 89 4C 24 08 55 56 57 41 54 41 55 41 56 41 57 48 83";
        private const string MapperWaitOnePattern =
            "41 83 F8 36 75 4D 48 63 C2 48 8D 15 ?? ?? ?? ?? 48 69 C8 3C 58 00 00";
        private const string MapperWaitTwoPattern =
            "48 63 C2 48 69 C8 3C 58 00 00 48 8D 05 ?? ?? ?? ?? 83 BC 01 B0 24 13 00 00";
        private const string MapperWaitThreePattern =
            "41 81 C0 60 FF FF FF 41 81 F8 A8 00 00 00 77 52 49 63 C0 4C 8D 05 ?? ?? ?? ??";
        private const string MapperWaitFourPattern =
            "41 81 C0 50 FF FF FF 41 81 F8 87 00 00 00 77 57 49 63 C0 4C 8D 05 ?? ?? ?? ??";
        private const string DeleteHovelPattern =
            "48 89 5C 24 08 57 48 83 EC 20 48 63 FA 48 8D 15 ?? ?? ?? ?? 48 69 CF 3C 58 00 00";
        private const string MaintenanceOnePattern =
            "48 8B C4 55 41 57 48 83 EC 68 48 63 EA 4C 8D 3D ?? ?? ?? ?? 48 69 CD 3C 58 00 00";
        private const string MaintenanceTwoPattern =
            "4C 8B DC 55 41 56 41 57 48 83 EC 60 4C 8D 3D ?? ?? ?? ?? 48 63 EA 48 69 D5 3C 58 00 00";
        private const string ActiveLayoutReferencePattern =
            "48 63 F2 48 8D 05 ?? ?? ?? ?? 4C 69 CE 3C 58 00 00";

        private const int AllocateSpecRva = 0x50680;
        private const int SetPlacementRva = 0x54EC0;
        private const int SelectBestFitRva = 0x54F60;
        private const int TestSpecificCandidateRva = 0x54DE0;
        private const int LoadCandidateRva = 0x55320;
        private const int ApplyRotationRva = 0x56670;
        private const int EvaluateCandidateFitRva = 0x57080;
        private const int PrepareLayoutRva = 0x53D00;
        private const int SchedulerRva = 0x539B0;
        private const int ExecuteBuildStepRva = 0x51790;
        private const int AlternativeExecutionRva = 0x52270;
        private const int PlacementHelperRva = 0x5CD90;
        private const int ValidatorRva = 0x7B060;
        private const int ActiveLayoutReferenceRva = 0x55F64;
        private const int ResourceGateRva = 0xCC420;
        private const int MapperWaitOneRva = 0x414A0;
        private const int MapperWaitTwoRva = 0x41230;
        private const int MapperWaitThreeRva = 0x41380;
        private const int MapperWaitFourRva = 0x41280;
        private const int DeleteHovelRva = 0x3B1D0;
        private const int MaintenanceOneRva = 0x50340;
        private const int MaintenanceTwoRva = 0x504F0;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int AllocateSpecDelegate(ulong state, int playerId);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SetPlacementDelegate(ulong state, int spec, int keepX, int keepY, int orientation);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SelectBestFitDelegate(ulong state, int spec, byte rotations);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate uint TestSpecificCandidateDelegate(ulong state, int spec, int candidate);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void LoadCandidateDelegate(ulong state, int zeroBasedPlayerId, int candidate);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void ApplyRotationDelegate(ulong state, int orientation);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int EvaluateCandidateFitDelegate(ulong state, int spec);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void PrepareLayoutDelegate(ulong state, int spec, int playerId);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SchedulerDelegate(ulong state, int playerId);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int ExecuteBuildStepDelegate(ulong state, int playerId, int frame, int restrictedMode, byte freeOrForced);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate long AlternativeExecutionDelegate(ulong state, int playerId, int pausedMode);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate long PlacementHelperDelegate(ulong manager, int playerId, int x, int y, int mapperValue, int orientation);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int ValidatorDelegate(ulong state, int tileId, int playerId, int mapperValue, int mode);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int ResourceGateDelegate(ulong manager, int mapperValue, int playerId, int mode);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int MapperGateDelegate(ulong manager, int playerId, int mapperValue);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int PlayerAiDelegate(ulong manager, int playerId);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void MaintenanceDelegate(ulong state, int playerId);

        private readonly ManualLogSource log;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly Dictionary<int, PlayerSession> players = new Dictionary<int, PlayerSession>();
        private readonly DetourHandle<AllocateSpecDelegate> allocateHook = new DetourHandle<AllocateSpecDelegate>();
        private readonly DetourHandle<SetPlacementDelegate> setPlacementHook = new DetourHandle<SetPlacementDelegate>();
        private readonly DetourHandle<SelectBestFitDelegate> selectHook = new DetourHandle<SelectBestFitDelegate>();
        private readonly DetourHandle<TestSpecificCandidateDelegate> specificHook = new DetourHandle<TestSpecificCandidateDelegate>();
        private readonly DetourHandle<LoadCandidateDelegate> loadHook = new DetourHandle<LoadCandidateDelegate>();
        private readonly DetourHandle<ApplyRotationDelegate> rotationHook = new DetourHandle<ApplyRotationDelegate>();
        private readonly DetourHandle<EvaluateCandidateFitDelegate> fitHook = new DetourHandle<EvaluateCandidateFitDelegate>();
        private readonly DetourHandle<PrepareLayoutDelegate> prepareHook = new DetourHandle<PrepareLayoutDelegate>();
        private readonly DetourHandle<SchedulerDelegate> schedulerHook = new DetourHandle<SchedulerDelegate>();
        private readonly DetourHandle<ExecuteBuildStepDelegate> executeHook = new DetourHandle<ExecuteBuildStepDelegate>();
        private readonly DetourHandle<AlternativeExecutionDelegate> alternativeHook = new DetourHandle<AlternativeExecutionDelegate>();
        private readonly DetourHandle<PlacementHelperDelegate> placementHook = new DetourHandle<PlacementHelperDelegate>();
        private readonly DetourHandle<ValidatorDelegate> validatorHook = new DetourHandle<ValidatorDelegate>();
        private readonly DetourHandle<ResourceGateDelegate> resourceGateHook = new DetourHandle<ResourceGateDelegate>();
        private readonly DetourHandle<MapperGateDelegate> mapperWaitOneHook = new DetourHandle<MapperGateDelegate>();
        private readonly DetourHandle<MapperGateDelegate> mapperWaitTwoHook = new DetourHandle<MapperGateDelegate>();
        private readonly DetourHandle<MapperGateDelegate> mapperWaitThreeHook = new DetourHandle<MapperGateDelegate>();
        private readonly DetourHandle<MapperGateDelegate> mapperWaitFourHook = new DetourHandle<MapperGateDelegate>();
        private readonly DetourHandle<PlayerAiDelegate> deleteHovelHook = new DetourHandle<PlayerAiDelegate>();
        private readonly DetourHandle<MaintenanceDelegate> maintenanceOneHook = new DetourHandle<MaintenanceDelegate>();
        private readonly DetourHandle<MaintenanceDelegate> maintenanceTwoHook = new DetourHandle<MaintenanceDelegate>();
        private HookTransaction transaction;
        private ulong activeLayoutIndexBase;
        private int mapSequence;
        private int nestedExecuteDepth;
        private ExecuteContext activeExecute;
        private int activeSelectionPlayerId;

        public PreplacedTestRuntime(ManualLogSource log) => this.log = log ?? throw new ArgumentNullException(nameof(log));

        public void InstallEventDiagnostics()
        {
            subscriptions.Add(MapLoaderR3EventHooks.OnLoadMap.Observable.Subscribe(a => OnMapLoad(a)));
            subscriptions.Add(MapLoaderR3EventHooks.OnStartMap.Observable.Subscribe(a => OnMapStart(a)));
            subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable.Subscribe(a => OnMapUnload(a)));
            subscriptions.Add(BuildingR3EventHooks.OnBuildStructure.Observable.Subscribe(OnBuildStructure));
            subscriptions.Add(BuildingR3EventHooks.OnBuildingSpawn.Observable.Subscribe(OnBuildingSpawn));
            subscriptions.Add(BuildingR3EventHooks.OnPlacementValidation.Observable.Subscribe(OnPlacementValidation));
            subscriptions.Add(BuildingR3EventHooks.OnBuildingTileTakeDamage.Observable.Subscribe(OnBuildingDamage));
            subscriptions.Add(BuildingR3EventHooks.OnBuildingBulldoze.Observable.Subscribe(OnBuildingBulldoze));
            subscriptions.Add(BuildingR3EventHooks.OnBuildingDelete.Observable.Subscribe(OnBuildingDelete));
            Shared.DebugLogHelper.LogInfo(log, "PREPLACED_EVENTS_READY: passive Script-Extender lifecycle/building diagnostics installed.");
        }

        public void TryInstallNativeDiagnostics(CrusaderLibraryLoadContext context, bool hashMatches)
        {
            if (!hashMatches)
            {
                Shared.DebugLogHelper.LogWarning(log,
                    "PREPLACED_NATIVE_INCOMPLETE: native hash differs; all native AIV hooks remain atomically disabled, event diagnostics continue.");
                return;
            }
            try
            {
                Dictionary<string, int> rvas = ResolveAll(context.Memory);
                activeLayoutIndexBase = ResolveRipAddress(context, rvas["active-layout-reference"] + 3, 3, 7);
                ulong module = unchecked((ulong)context.ModuleHandle.ToInt64());
                transaction = new HookTransaction(context.Region,
                    SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                    new HookTransactionOptions { FailureMode = TransactionFailureMode.RollbackAndThrow, OwnsHooks = false });
                transaction.AddDetour(allocateHook, HookTarget.FromAddress(module + (ulong)rvas["allocate"]), AllocateSpec);
                transaction.AddDetour(setPlacementHook, HookTarget.FromAddress(module + (ulong)rvas["set-placement"]), SetPlacement);
                transaction.AddDetour(selectHook, HookTarget.FromAddress(module + (ulong)rvas["select"]), SelectBestFit);
                transaction.AddDetour(specificHook, HookTarget.FromAddress(module + (ulong)rvas["specific"]), TestSpecificCandidate);
                transaction.AddDetour(loadHook, HookTarget.FromAddress(module + (ulong)rvas["load"]), LoadCandidate);
                transaction.AddDetour(rotationHook, HookTarget.FromAddress(module + (ulong)rvas["rotation"]), ApplyRotation);
                transaction.AddDetour(fitHook, HookTarget.FromAddress(module + (ulong)rvas["fit"]), EvaluateCandidateFit);
                transaction.AddDetour(prepareHook, HookTarget.FromAddress(module + (ulong)rvas["prepare"]), PrepareLayout);
                transaction.AddDetour(schedulerHook, HookTarget.FromAddress(module + (ulong)rvas["scheduler"]), Scheduler);
                transaction.AddDetour(executeHook, HookTarget.FromAddress(module + (ulong)rvas["execute"]), ExecuteBuildStep);
                transaction.AddDetour(alternativeHook, HookTarget.FromAddress(module + (ulong)rvas["alternative"]), AlternativeExecution);
                transaction.AddDetour(placementHook, HookTarget.FromAddress(module + (ulong)rvas["placement-helper"]), PlacementHelper);
                transaction.AddDetour(validatorHook, HookTarget.FromAddress(module + (ulong)rvas["validator"]), Validator);
                transaction.AddDetour(resourceGateHook, HookTarget.FromAddress(module + (ulong)rvas["resource-gate"]), ResourceGate);
                transaction.AddDetour(mapperWaitOneHook, HookTarget.FromAddress(module + (ulong)rvas["mapper-wait-1"]), MapperWaitOne);
                transaction.AddDetour(mapperWaitTwoHook, HookTarget.FromAddress(module + (ulong)rvas["mapper-wait-2"]), MapperWaitTwo);
                transaction.AddDetour(mapperWaitThreeHook, HookTarget.FromAddress(module + (ulong)rvas["mapper-wait-3"]), MapperWaitThree);
                transaction.AddDetour(mapperWaitFourHook, HookTarget.FromAddress(module + (ulong)rvas["mapper-wait-4"]), MapperWaitFour);
                transaction.AddDetour(deleteHovelHook, HookTarget.FromAddress(module + (ulong)rvas["delete-hovel"]), DeleteHovel);
                transaction.AddDetour(maintenanceOneHook, HookTarget.FromAddress(module + (ulong)rvas["maintenance-1"]), MaintenanceOne);
                transaction.AddDetour(maintenanceTwoHook, HookTarget.FromAddress(module + (ulong)rvas["maintenance-2"]), MaintenanceTwo);
                CommitResult result = transaction.Commit();
                if (!result.IsCompleteSuccess || !AllHooksSucceeded())
                    throw new InvalidOperationException("atomic native hook transaction was incomplete: " + result);
                Shared.DebugLogHelper.LogInfo(log,
                    $"PREPLACED_NATIVE_READY: 21 passive detours installed atomically; activeLayoutBase=0x{activeLayoutIndexBase:X}.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log,
                    $"PREPLACED_NATIVE_INCOMPLETE: signature/ABI/address validation failed; native hook set rolled back, event diagnostics continue. {ex}");
            }
        }

        private Dictionary<string, int> ResolveAll(ReadOnlySpan<byte> memory)
        {
            var definitions = new[]
            {
                Def("allocate", AllocateSpecPattern, AllocateSpecRva), Def("set-placement", SetPlacementPattern, SetPlacementRva),
                Def("select", SelectBestFitPattern, SelectBestFitRva), Def("specific", TestSpecificCandidatePattern, TestSpecificCandidateRva),
                Def("load", LoadCandidatePattern, LoadCandidateRva), Def("rotation", ApplyRotationPattern, ApplyRotationRva),
                Def("fit", EvaluateCandidateFitPattern, EvaluateCandidateFitRva), Def("prepare", PrepareLayoutPattern, PrepareLayoutRva),
                Def("scheduler", SchedulerPattern, SchedulerRva), Def("execute", ExecuteBuildStepPattern, ExecuteBuildStepRva),
                Def("alternative", AlternativeExecutionPattern, AlternativeExecutionRva),
                Def("placement-helper", PlacementHelperPattern, PlacementHelperRva), Def("validator", ValidatorPattern, ValidatorRva),
                Def("resource-gate", ResourceGatePattern, ResourceGateRva),
                Def("mapper-wait-1", MapperWaitOnePattern, MapperWaitOneRva), Def("mapper-wait-2", MapperWaitTwoPattern, MapperWaitTwoRva),
                Def("mapper-wait-3", MapperWaitThreePattern, MapperWaitThreeRva), Def("mapper-wait-4", MapperWaitFourPattern, MapperWaitFourRva),
                Def("delete-hovel", DeleteHovelPattern, DeleteHovelRva), Def("maintenance-1", MaintenanceOnePattern, MaintenanceOneRva),
                Def("maintenance-2", MaintenanceTwoPattern, MaintenanceTwoRva),
                Def("active-layout-reference", ActiveLayoutReferencePattern, ActiveLayoutReferenceRva)
            };
            Dictionary<string, int> result = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (NativeDefinition definition in definitions)
                result.Add(definition.Name, Shared.NativePatternResolver.ResolveUnique(memory, definition.Pattern,
                    definition.Rva, true, "PreplacedTest " + definition.Name, log).Rva);
            return result;
        }

        private static NativeDefinition Def(string name, string pattern, int rva) => new NativeDefinition(name, pattern, rva);

        private static ulong ResolveRipAddress(CrusaderLibraryLoadContext context, int instructionRva, int displacementOffset, int length)
        {
            int targetRva = Shared.NativePatternResolver.ResolveRelativeTarget(context.Memory,
                instructionRva + displacementOffset, instructionRva + length);
            if (targetRva < 0 || targetRva >= context.Memory.Length)
                throw new InvalidOperationException("RIP-relative active-layout target is outside the image.");
            return unchecked((ulong)context.ModuleHandle.ToInt64()) + (ulong)targetRva;
        }

        private bool AllHooksSucceeded() => allocateHook.Success && setPlacementHook.Success && selectHook.Success &&
            specificHook.Success && loadHook.Success && rotationHook.Success && fitHook.Success && prepareHook.Success &&
            schedulerHook.Success && executeHook.Success && alternativeHook.Success && placementHook.Success && validatorHook.Success &&
            resourceGateHook.Success && mapperWaitOneHook.Success && mapperWaitTwoHook.Success && mapperWaitThreeHook.Success &&
            mapperWaitFourHook.Success && deleteHovelHook.Success && maintenanceOneHook.Success && maintenanceTwoHook.Success;

        private int AllocateSpec(ulong state, int playerId)
        {
            int result = allocateHook.Original(state, playerId);
            Safe(() => { Session(playerId).Counters.Add("phase.allocate-spec result=" + result); Immediate(playerId, $"AIV_SPEC_ALLOCATED: spec={result}"); });
            return result;
        }

        private void SetPlacement(ulong state, int spec, int keepX, int keepY, int orientation)
        {
            setPlacementHook.Original(state, spec, keepX, keepY, orientation);
            Safe(() => { int playerId = ReadSpec(state, spec, PlayerIdOffset); Session(playerId).Counters.Add("phase.set-placement");
                Immediate(playerId, $"AIV_PLACEMENT_SET: spec={spec}, keep=({keepX},{keepY}), orientation={orientation}, {DescribeSpec(state, spec)}"); });
        }

        private void SelectBestFit(ulong state, int spec, byte rotations)
        {
            int old = activeSelectionPlayerId; activeSelectionPlayerId = SafePlayerFromSpec(state, spec);
            Session(activeSelectionPlayerId).Counters.Add("phase.select-best-fit");
            try { selectHook.Original(state, spec, rotations); }
            finally { Safe(() => Immediate(activeSelectionPlayerId, $"AIV_SELECTION_COMPLETE: rotations={rotations}, {DescribeSpec(state, spec)}")); activeSelectionPlayerId = old; }
        }

        private uint TestSpecificCandidate(ulong state, int spec, int candidate)
        {
            int playerId = SafePlayerFromSpec(state, spec); Session(playerId).Counters.Add("phase.test-specific-candidate candidate=" + candidate);
            uint result = specificHook.Original(state, spec, candidate);
            Safe(() => Immediate(playerId, $"AIV_SPECIFIC_RESULT: candidate={candidate}, result={result}, {DescribeSpec(state, spec)}"));
            return result;
        }

        private void LoadCandidate(ulong state, int zeroBasedPlayerId, int candidate)
        {
            loadHook.Original(state, zeroBasedPlayerId, candidate);
            int playerId = zeroBasedPlayerId + 1; Session(playerId).Counters.Add("phase.load-candidate candidate=" + candidate);
        }

        private void ApplyRotation(ulong state, int orientation)
        {
            rotationHook.Original(state, orientation);
            Session(activeSelectionPlayerId).Counters.Add("phase.apply-rotation orientation=" + orientation);
        }

        private int EvaluateCandidateFit(ulong state, int spec)
        {
            int result = fitHook.Original(state, spec); int playerId = SafePlayerFromSpec(state, spec);
            Session(playerId).Counters.Add("candidate-fit result=" + result + " candidate=" + ReadSpec(state, spec, CandidateIdOffset));
            return result;
        }

        private void PrepareLayout(ulong state, int spec, int playerId)
        {
            List<BuildingSnapshot> before = CaptureBuildings(playerId);
            Session(playerId).Counters.Add("phase.prepare-layout");
            EmitBuildingInventory(playerId, "PREPARE_BEFORE", before, null);
            prepareHook.Original(state, spec, playerId);
            Safe(() =>
            {
                int originX = ReadSpec(state, spec, OriginXOffset), originY = ReadSpec(state, spec, OriginYOffset);
                EmitBuildingInventory(playerId, "PREPARE_TRANSLATED", before, b =>
                    b.TileX >= originX && b.TileX < originX + AivGridSize && b.TileY >= originY && b.TileY < originY + AivGridSize);
                Immediate(playerId, "AIV_LAYOUT_PREPARED: " + DescribeSpec(state, spec));
            });
        }

        private void Scheduler(ulong state, int playerId)
        {
            PlayerSession session = Session(playerId); SchedulerGateState before = ReadSchedulerState(state, playerId);
            string predictedGate = SchedulerGateClassifier.ClassifyBeforeCall(before);
            int executeBefore = session.NestedExecuteCalls, alternateBefore = session.AlternativeCalls;
            session.Counters.Add("scheduler.call"); session.Counters.Add("scheduler.pre=" + predictedGate);
            schedulerHook.Original(state, playerId);
            Safe(() =>
            {
                SchedulerGateState after = ReadSchedulerState(state, playerId);
                string reached = session.NestedExecuteCalls != executeBefore ? "execute-build-step" :
                    session.AlternativeCalls != alternateBefore ? "alternative-execution" : "no-aiv-execution";
                session.Counters.Add("scheduler.reached=" + reached);
                session.Counters.Add("economy.phase=" + ReadPlayerGlobal(playerId, EconomyPhaseRelativeOffset));
                if (before.CrushedCounter != after.CrushedCounter)
                    Immediate(playerId, $"CRUSHED_TIMER_CHANGE: {before.CrushedCounter}->{after.CrushedCounter}, configured={before.CrushedDelay}");
                FlushIfDue(playerId, state, after);
            });
        }

        private int ExecuteBuildStep(ulong state, int playerId, int frame, int restrictedMode, byte freeOrForced)
        {
            PlayerSession session = Session(playerId); session.NestedExecuteCalls++; nestedExecuteDepth++;
            FrameSnapshot before = ReadFrame(state, playerId, frame);
            ExecuteContext previous = activeExecute;
            ExecuteContext current = new ExecuteContext(playerId, frame, before.Status);
            activeExecute = current;
            int result;
            try { result = executeHook.Original(state, playerId, frame, restrictedMode, freeOrForced); }
            finally { activeExecute = previous; nestedExecuteDepth--; }
            Safe(() =>
            {
                FrameSnapshot after = ReadFrame(state, playerId, frame);
                string reason = ClassifyExecuteResult(result, current, before, after);
                session.Counters.Add($"execute.result={result} reason={reason} mapper={before.Mapper}");
                session.Counters.Add($"execute.frame={frame} status={before.Status}->{after.Status} mode={restrictedMode}/{freeOrForced}");
                bool confirmed = session.FirstBuilding.TryConfirm(DateTime.UtcNow, result != 0,
                    current.SpawnedBuildingIds.Count != 0, before.Status != after.Status);
                if (confirmed)
                    Immediate(playerId, $"FIRST_AIV_BUILDING_CONFIRMED: frame={frame}, mapper={before.Mapper}, pos={before.Position}, result={result}, spawned=[{string.Join(",", current.SpawnedBuildingIds)}], status={before.Status}->{after.Status}; followUpSeconds=10");
            });
            return result;
        }

        private long AlternativeExecution(ulong state, int playerId, int pausedMode)
        {
            PlayerSession session = Session(playerId); session.AlternativeCalls++; session.Counters.Add("phase.alternative-execution mode=" + pausedMode);
            return alternativeHook.Original(state, playerId, pausedMode);
        }

        private long PlacementHelper(ulong manager, int playerId, int x, int y, int mapperValue, int orientation)
        {
            long result = placementHook.Original(manager, playerId, x, y, mapperValue, orientation);
            Session(playerId).Counters.Add($"placement-helper result={result} mapper={(eMappers)mapperValue} pos=({x},{y}) orientation={orientation}");
            if (activeExecute != null && activeExecute.PlayerId == playerId) activeExecute.PlacementResults.Add(result);
            return result;
        }

        private int Validator(ulong state, int tileId, int playerId, int mapperValue, int mode)
        {
            int result = validatorHook.Original(state, tileId, playerId, mapperValue, mode);
            Session(playerId).Counters.Add($"native-validator result={result} mapper={(eMappers)mapperValue} tileId={tileId} mode={mode}");
            if (activeExecute != null && activeExecute.PlayerId == playerId) activeExecute.ValidatorResults.Add(result);
            return result;
        }

        private int ResourceGate(ulong manager, int mapperValue, int playerId, int mode)
        {
            int result = resourceGateHook.Original(manager, mapperValue, playerId, mode);
            Session(playerId).Counters.Add($"resource-gate result={result} mapper={(eMappers)mapperValue} mode={mode}");
            if (activeExecute != null && activeExecute.PlayerId == playerId) activeExecute.ResourceResults.Add(result);
            return result;
        }

        private int MapperWaitOne(ulong manager, int playerId, int mapperValue) => RecordMapperGate(mapperWaitOneHook, "mapper-wait-1", manager, playerId, mapperValue);
        private int MapperWaitTwo(ulong manager, int playerId, int mapperValue) => RecordMapperGate(mapperWaitTwoHook, "mapper-wait-2", manager, playerId, mapperValue);
        private int MapperWaitThree(ulong manager, int playerId, int mapperValue) => RecordMapperGate(mapperWaitThreeHook, "mapper-wait-3", manager, playerId, mapperValue);
        private int MapperWaitFour(ulong manager, int playerId, int mapperValue) => RecordMapperGate(mapperWaitFourHook, "mapper-wait-4", manager, playerId, mapperValue);

        private int RecordMapperGate(DetourHandle<MapperGateDelegate> hook, string name, ulong manager, int playerId, int mapperValue)
        {
            int result = hook.Original(manager, playerId, mapperValue);
            Session(playerId).Counters.Add($"{name} result={result} mapper={(eMappers)mapperValue}");
            if (activeExecute != null && activeExecute.PlayerId == playerId && result != 0) activeExecute.WaitRejectors.Add(name);
            return result;
        }

        private int DeleteHovel(ulong manager, int playerId)
        {
            int result = deleteHovelHook.Original(manager, playerId);
            Session(playerId).Counters.Add("hovel-delete result=" + result);
            if (result != 0) Immediate(playerId, "HOVEL_DELETE_SUCCEEDED");
            return result;
        }

        private void MaintenanceOne(ulong state, int playerId)
        {
            maintenanceOneHook.Original(state, playerId);
            Session(playerId).Counters.Add("maintenance.0x50340");
        }

        private void MaintenanceTwo(ulong state, int playerId)
        {
            maintenanceTwoHook.Original(state, playerId);
            Session(playerId).Counters.Add("maintenance.0x504F0");
        }

        private string ClassifyExecuteResult(int result, ExecuteContext context, FrameSnapshot before, FrameSnapshot after)
        {
            if (result != 0) return context.SpawnedBuildingIds.Count != 0 ? "success-building-spawn" :
                before.Status != after.Status ? "success-frame-advanced-no-building" : "success-command-or-nonbuilding";
            if (context.ResourceResults.Any(v => v == 0)) return "insufficient-resources-or-resource-gate";
            if (context.WaitRejectors.Count != 0) return "mapper-wait-gate:" + string.Join(",", context.WaitRejectors);
            if (context.ValidatorResults.Any(v => v == 0)) return "placement-validator-rejected";
            if (context.PlacementResults.Any(v => v == 0)) return "placement-helper-rejected";
            if (context.ValidatorResults.Count == 0 && context.PlacementResults.Count == 0) return "rejected-before-placement-helper";
            return "placement-failed-unspecified";
        }

        private void OnMapLoad(MapLoadEventArgs args)
        {
            if (args.Phase == EventHookPhase.Pre) ResetMap("OnLoadMap(Pre)");
            Shared.DebugLogHelper.LogInfo(log, $"PREPLACED_MAP_LOAD: phase={args.Phase}, sequence={mapSequence}.");
        }

        private void OnMapStart(MapStartEventArgs args)
        {
            if (args.Phase == EventHookPhase.Post)
            {
                for (int playerId = 1; playerId <= MaxPlayablePlayerId; playerId++)
                    if (IsAi(playerId)) Session(playerId).Counters.Add("lifecycle.map-start");
            }
            Shared.DebugLogHelper.LogInfo(log, $"PREPLACED_MAP_START: phase={args.Phase}, sequence={mapSequence}.");
        }

        private void OnMapUnload(MapUnloadEventArgs args)
        {
            if (args.Phase != EventHookPhase.Pre) return;
            foreach (int playerId in players.Keys.ToArray()) FinalizePlayer(playerId, "map-unload");
        }

        private void OnBuildStructure(BuildStructureEventArgs args)
        {
            if (!IsAi(args.PlayerId)) return;
            Session(args.PlayerId).Counters.Add($"event.build-structure phase={args.Phase} mapper={args.Mappers} pos=({args.TileX},{args.TileY}) free={args.IsFree}");
        }

        private void OnBuildingSpawn(BuildingSpawnEventArgs args)
        {
            if (args.Phase != EventHookPhase.Post || !IsAi(args.PlayerId)) return;
            PlayerSession session = Session(args.PlayerId);
            session.Counters.Add($"event.spawn type={args.Building} pos=({args.TileX},{args.TileY}) result={args.ReturnValue}");
            if (activeExecute != null && activeExecute.PlayerId == args.PlayerId && args.ReturnValue > 0 && args.ReturnValue <= int.MaxValue)
                activeExecute.SpawnedBuildingIds.Add((int)args.ReturnValue);
        }

        private void OnPlacementValidation(BuildingPlacementValidationEventArgs args)
        {
            if (!IsAi(args.PlayerId)) return;
            Session(args.PlayerId).Counters.Add($"event.placement-validation phase={args.Phase} mapper={args.Mappers} pos=({args.TileX},{args.TileY}) custom={args.CustomValidationRules} forceBlock={args.ForceBlockPlacementState}");
        }

        private void OnBuildingDamage(BuildingTileTakeDamageEventArgs args)
        {
            int playerId = activeExecute?.PlayerId ?? args.PlayerIdSource;
            if (IsAi(playerId)) Session(playerId).Counters.Add($"event.damage phase={args.Phase} tile={args.TileId}@({args.TileX},{args.TileY}) amount={args.Damage} source={args.PlayerIdSource}");
        }

        private void OnBuildingBulldoze(BuildingBulldozeEventArgs args) => RecordRemoval("bulldoze", args.Phase, args.BuildingId);
        private void OnBuildingDelete(BuildingDeleteEventArgs args) => RecordRemoval("delete", args.Phase, args.BuildingId);

        private void RecordRemoval(string kind, EventHookPhase phase, int buildingId)
        {
            if (phase != EventHookPhase.Pre || !TryBuildingOwner(buildingId, out int playerId) || !IsAi(playerId)) return;
            Session(playerId).Counters.Add($"event.{kind} buildingId={buildingId}");
            Immediate(playerId, $"BUILDING_REMOVAL: kind={kind}, buildingId={buildingId}");
        }

        private void ResetMap(string reason)
        {
            if (players.Count != 0)
                foreach (int playerId in players.Keys.ToArray()) FinalizePlayer(playerId, "map-transition");
            players.Clear(); mapSequence++; activeExecute = null; activeSelectionPlayerId = 0;
            Shared.DebugLogHelper.LogInfo(log, $"PREPLACED_SESSION_RESET: reason={reason}, sequence={mapSequence}.");
        }

        private PlayerSession Session(int playerId)
        {
            if (!players.TryGetValue(playerId, out PlayerSession session))
            {
                session = new PlayerSession(playerId); players.Add(playerId, session);
                Immediate(playerId, "AI_OBSERVATION_STARTED");
            }
            return session;
        }

        private void FlushIfDue(int playerId, ulong state, SchedulerGateState gate)
        {
            PlayerSession session = Session(playerId); DateTime now = DateTime.UtcNow;
            if (now < session.NextFlushUtc) return;
            session.NextFlushUtc = now.AddSeconds(1);
            int pauseIndex = ReadPlayerGlobal(playerId, PauseIndexRelativeOffset);
            string stateText = $"state activeAiv={gate.ActiveAivSlot}, activeAic={ReadPlayerGlobal(playerId, ActiveAicRelativeOffset)}, crushed={gate.CrushedCounter}/{gate.CrushedDelay}, gold={gate.Gold}, build={gate.BuildCounter}/{gate.BuildRate}, pause={gate.PauseCounter}, pauseIndex={pauseIndex}, pauseThreshold={ReadPlayerInt16(playerId, PauseTableRelativeOffset, pauseIndex)}, pauseConfigured={ReadPlayerGlobal(playerId, PauseConfiguredRelativeOffset)}, economyPhase={ReadPlayerGlobal(playerId, EconomyPhaseRelativeOffset)}, goal={gate.CurrentStepGoal}, highest={gate.HighestPreparedFrame}";
            EmitCounters(playerId, "INTERVAL", session.Counters.DrainInterval(), stateText);
            if (!session.Finalized && session.FirstBuilding.FollowUpComplete(now)) FinalizePlayer(playerId, "first-building-follow-up-complete");
        }

        private void FinalizePlayer(int playerId, string reason)
        {
            if (!players.TryGetValue(playerId, out PlayerSession session) || session.Finalized) return;
            session.Finalized = true;
            KeyValuePair<string, long>[] remaining = session.Counters.DrainInterval();
            if (remaining.Length != 0) EmitCounters(playerId, "FINAL_PENDING", remaining, "reason=" + reason);
            EmitCounters(playerId, "FINAL_TOTAL", session.Counters.SnapshotTotal(), "reason=" + reason);
            session.Counters.Stop();
        }

        private void EmitCounters(int playerId, string label, IEnumerable<KeyValuePair<string, long>> counters, string prefix)
        {
            string payload = prefix + "; " + string.Join("; ", counters.Select(p => p.Key + "=" + p.Value));
            EmitChunked($"PREPLACED_{label}: player={playerId}; ", payload);
        }

        private void EmitBuildingInventory(int playerId, string label, IList<BuildingSnapshot> buildings, Func<BuildingSnapshot, bool> inArea)
        {
            IEnumerable<string> items = buildings.Select(b => b.ToText(inArea == null ? (bool?)null : inArea(b)));
            EmitChunked($"PREPLACED_{label}: player={playerId}; count={buildings.Count}; ", string.Join("; ", items));
        }

        private void EmitChunked(string prefix, string payload)
        {
            if (payload.Length == 0) { Shared.DebugLogHelper.LogInfo(log, prefix + "<empty>"); return; }
            string[] chunks = DiagnosticChunker.Split(payload, LogPayloadLength);
            for (int index = 0; index < chunks.Length; index++)
            {
                Shared.DebugLogHelper.LogInfo(log, $"{prefix}part={index + 1}/{chunks.Length}; {chunks[index]}");
            }
        }

        private void Immediate(int playerId, string text) => Shared.DebugLogHelper.LogInfo(log, $"PREPLACED_EVENT: player={playerId}; {text}");

        private SchedulerGateState ReadSchedulerState(ulong state, int playerId)
        {
            int active = ReadPlayerGlobal(playerId, 0), crushed = ReadPlayerGlobal(playerId, CrushedCounterRelativeOffset);
            int crushedDelay = 0;
            try
            {
                int activeAic = ReadPlayerGlobal(playerId, ActiveAicRelativeOffset);
                var aics = GameAIManagerAPI.Instance.GetAICArray();
                if (activeAic >= 0 && activeAic < aics.Length)
                    crushedDelay = aics.GetValue(activeAic).crushed_building_delay;
            }
            catch { }
            int gold = 0;
            try { gold = GamePlayerManagerAPI.Instance.GetPlayerGold(playerId); } catch { }
            if (active <= 0 || active > MaxAivSpecIndex)
                return new SchedulerGateState(active, crushed, crushedDelay, gold, 0, 0,
                    ReadPlayerGlobal(playerId, PauseCounterRelativeOffset), 0, 0);
            byte* spec = (byte*)state + active * AivSpecStride;
            return new SchedulerGateState(active, crushed, crushedDelay, gold,
                *(int*)(spec + BuildCounterOffset), *(int*)(spec + BuildRateOffset),
                ReadPlayerGlobal(playerId, PauseCounterRelativeOffset), *(int*)(spec + CurrentStepGoalOffset),
                *(int*)(spec + HighestPreparedFrameOffset));
        }

        private FrameSnapshot ReadFrame(ulong state, int playerId, int frame)
        {
            int active = ReadPlayerGlobal(playerId, 0);
            if (active <= 0 || active > MaxAivSpecIndex || frame < 0 || frame >= PreparedLayoutFrameCount)
                return new FrameSnapshot(frame, -1, 0, 0, 0, "invalid");
            byte* entry = (byte*)state + PreparedEntryBaseOffset + ((active * PreparedLayoutFrameCount + frame) * PreparedEntrySize);
            int firstPosition = *(int*)(entry + 8);
            return new FrameSnapshot(frame, *entry, *(short*)(entry + 2), *(short*)(entry + 4), firstPosition,
                $"firstPositionIndex={firstPosition}");
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

        private static int ReadSpec(ulong state, int spec, int offset) =>
            state == 0 || spec < 0 || spec > MaxAivSpecIndex ? 0 : *(int*)((byte*)state + spec * AivSpecStride + offset);

        private static int SafePlayerFromSpec(ulong state, int spec) => ReadSpec(state, spec, PlayerIdOffset);

        private static string DescribeSpec(ulong state, int spec) =>
            $"spec={spec}, player={ReadSpec(state, spec, PlayerIdOffset)}, candidate={ReadSpec(state, spec, CandidateIdOffset)}, orientation={ReadSpec(state, spec, OrientationOffset)}, placementState={ReadSpec(state, spec, PlacementStateOffset)}, origin=({ReadSpec(state, spec, OriginXOffset)},{ReadSpec(state, spec, OriginYOffset)}), keep=({ReadSpec(state, spec, KeepXOffset)},{ReadSpec(state, spec, KeepYOffset)}), goal={ReadSpec(state, spec, CurrentStepGoalOffset)}, highest={ReadSpec(state, spec, HighestPreparedFrameOffset)}";

        private List<BuildingSnapshot> CaptureBuildings(int playerId)
        {
            List<BuildingSnapshot> result = new List<BuildingSnapshot>();
            Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            for (int spanIndex = 0; spanIndex < buildings.Length; spanIndex++)
            {
                ref GameBuilding building = ref buildings[spanIndex];
                if (building.r_PlayerIdOwner != playerId || (building.r_AliveState != AliveState.IsAlive && building.r_AliveState != AliveState.NeedsInit)) continue;
                result.Add(new BuildingSnapshot(spanIndex + 1, building.r_GlobalId, building.r_BuildingType,
                    building.r_AliveState, building.r_TilePositionXBegin, building.r_TilePositionYBegin,
                    building.r_TilePositionXEnd, building.r_TilePositionYEnd, building.r_WorldPositionX, building.r_WorldPositionY));
            }
            return result;
        }

        private bool TryBuildingOwner(int buildingId, out int playerId)
        {
            playerId = 0;
            if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building)) return false;
            playerId = building->r_PlayerIdOwner; return true;
        }

        private bool IsAi(int playerId)
        {
            try { return playerId >= 1 && playerId <= MaxPlayablePlayerId && GamePlayerManagerAPI.Instance.IsAIPlayer(playerId); }
            catch { return false; }
        }

        private void Safe(Action action)
        {
            try { action(); }
            catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "PREPLACED_CALLBACK_ERROR: Vanilla result was preserved. " + ex); }
        }

        private readonly struct NativeDefinition
        {
            public NativeDefinition(string name, string pattern, int rva) { Name = name; Pattern = pattern; Rva = rva; }
            public string Name { get; } public string Pattern { get; } public int Rva { get; }
        }

        private sealed class PlayerSession
        {
            public PlayerSession(int playerId) { PlayerId = playerId; NextFlushUtc = DateTime.UtcNow.AddSeconds(1); FirstBuilding = new FirstBuildingWindow(TimeSpan.FromSeconds(10)); }
            public int PlayerId { get; } public DiagnosticCounterSet Counters { get; } = new DiagnosticCounterSet();
            public FirstBuildingWindow FirstBuilding { get; } public DateTime NextFlushUtc { get; set; }
            public int NestedExecuteCalls { get; set; } public int AlternativeCalls { get; set; } public bool Finalized { get; set; }
        }

        private sealed class ExecuteContext
        {
            public ExecuteContext(int playerId, int frame, int status) { PlayerId = playerId; Frame = frame; Status = status; }
            public int PlayerId { get; } public int Frame { get; } public int Status { get; }
            public List<int> SpawnedBuildingIds { get; } = new List<int>(); public List<long> PlacementResults { get; } = new List<long>();
            public List<int> ValidatorResults { get; } = new List<int>(); public List<int> ResourceResults { get; } = new List<int>();
            public List<string> WaitRejectors { get; } = new List<string>();
        }

        private readonly struct FrameSnapshot
        {
            public FrameSnapshot(int frame, int status, int mapper, int count, int first, string position) { Frame = frame; Status = status; Mapper = mapper; PositionCount = count; FirstPosition = first; Position = position; }
            public int Frame { get; } public int Status { get; } public int Mapper { get; } public int PositionCount { get; } public int FirstPosition { get; } public string Position { get; }
        }

        private readonly struct BuildingSnapshot
        {
            public BuildingSnapshot(int id, uint globalId, eStructs type, AliveState alive, int x, int y, int endX, int endY, int worldX, int worldY)
            { Id = id; GlobalId = globalId; Type = type; Alive = alive; TileX = x; TileY = y; EndX = endX; EndY = endY; WorldX = worldX; WorldY = worldY; }
            public int Id { get; } public uint GlobalId { get; } public eStructs Type { get; } public AliveState Alive { get; }
            public int TileX { get; } public int TileY { get; } public int EndX { get; } public int EndY { get; } public int WorldX { get; } public int WorldY { get; }
            public string ToText(bool? inArea) => $"id={Id},global={GlobalId},type={Type},alive={Alive},tile=({TileX},{TileY})-({EndX},{EndY}),world=({WorldX},{WorldY}),area={(inArea.HasValue ? (inArea.Value ? "inside" : "outside") : "pending")}";
        }
    }
}
