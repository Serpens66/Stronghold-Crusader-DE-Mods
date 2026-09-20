using BugfixesAndQoL;
using System;
using System.IO;
using System.Linq;

namespace BugfixesAndQoL
{
    internal static class CorruptLordDataSpawnTests
    {
        internal static void Run(Action<bool, string> check)
        {
            check(Evaluate() == CorruptLordDataSpawnDecision.ClearStaleLordReference,
                "exact corrupt-Lord tombstone is repaired");
            CheckEveryGuard(check);
            CheckSessionState(check);
            CheckRuntimeIntegration(check);
        }

        private static void CheckEveryGuard(Action<bool, string> check)
        {
            check(Evaluate(enabled: false) == CorruptLordDataSpawnDecision.RejectDisabled, "Lord fix setting guard");
            check(Evaluate(isNewGameSession: false) == CorruptLordDataSpawnDecision.RejectNotNewGameSession, "Lord fix new-game guard");
            check(Evaluate(correctionWindowOpen: false) == CorruptLordDataSpawnDecision.RejectOutsideCorrectionWindow, "Lord fix tick-window guard");
            check(Evaluate(hasPlayerRecord: false) == CorruptLordDataSpawnDecision.RejectMissingPlayerRecord, "Lord fix player-record guard");
            check(Evaluate(kicked: true) == CorruptLordDataSpawnDecision.RejectKicked, "Lord fix kicked-player guard");
            check(Evaluate(alreadyAttempted: true) == CorruptLordDataSpawnDecision.RejectAlreadyAttempted, "Lord fix single-attempt guard");
            check(Evaluate(isDefeated: true) == CorruptLordDataSpawnDecision.RejectDefeated, "Lord fix defeat-state guard");
            check(Evaluate(validOwnedKeep: false) == CorruptLordDataSpawnDecision.RejectInvalidKeep, "Lord fix Keep guard");
            check(Evaluate(validOwnedKeepDoorReference: false) == CorruptLordDataSpawnDecision.RejectInvalidKeepDoorReference, "Lord fix start-marker guard");
            check(Evaluate(lordUnitId: 0) == CorruptLordDataSpawnDecision.RejectMissingStoredLordUnitId, "Lord fix stored unit-ID guard");
            check(Evaluate(lordGlobalId: 0) == CorruptLordDataSpawnDecision.RejectMissingStoredLordGlobalId, "Lord fix stored global-ID guard");
            check(Evaluate(lordUnitResolved: false) == CorruptLordDataSpawnDecision.RejectLordUnitUnresolved, "Lord fix missing unit-slot guard");
            check(Evaluate(lordOwnerPlayerId: 7) == CorruptLordDataSpawnDecision.RejectLordOwnerNotZero, "Lord fix zero-owner guard");
            check(Evaluate(lordTypeIsNull: false) == CorruptLordDataSpawnDecision.RejectLordTypeNotNull, "Lord fix null-type guard");
            check(Evaluate(lordAliveStateIsNone: false) == CorruptLordDataSpawnDecision.RejectLordAliveStateNotNone, "Lord fix empty-state guard");
            check(Evaluate(lordUnitGlobalId: 517) == CorruptLordDataSpawnDecision.RejectLordUnitGlobalIdNotZero, "Lord fix empty-global-ID guard");
            check(Evaluate(lordCurrentHealth: 1) == CorruptLordDataSpawnDecision.RejectLordHealthNotZero, "Lord fix zero-health guard");

            CorruptLordDataSpawnDecision validLord = Evaluate(
                lordOwnerPlayerId: 7,
                lordTypeIsNull: false,
                lordAliveStateIsNone: false,
                lordUnitGlobalId: 534,
                lordCurrentHealth: 150000);
            check(validLord != CorruptLordDataSpawnDecision.ClearStaleLordReference,
                "valid Lord is never cleared");
        }

        private static void CheckSessionState(Action<bool, string> check)
        {
            var state = new CorruptLordDataSpawnSessionState();
            state.Reset(10);
            check(state.MarkAttempted(8) && !state.MarkAttempted(8) && state.WasAttempted(8),
                "Lord fix performs at most one attempt per player and session");
            check(state.MarkConfirmed(8) && !state.MarkConfirmed(8) && state.IsConfirmed(8),
                "Lord fix records Vanilla confirmation once");
            state.Reset(11);
            check(state.SessionId == 11 && !state.WasAttempted(8) && !state.IsConfirmed(8),
                "Lord fix session reset clears attempt and confirmation state");
        }

        private static void CheckRuntimeIntegration(Action<bool, string> check)
        {
            string runtime = File.ReadAllText(Path.Combine("src", "CorruptLordDataSpawnRuntime.cs"));
            string policy = File.ReadAllText(Path.Combine("src", "CorruptLordDataSpawnPolicy.cs"));
            string viewModel = File.ReadAllText(Path.Combine("src", "BugfixesAndQoLViewModel.cs"));
            string orchestrator = File.ReadAllText(Path.Combine("src", "BugfixesAndQoLRuntime.cs"));
            string xaml = File.ReadAllText(Path.Combine("Override", "ScriptExtenderUI", "BugfixesAndQoLSettings.xaml"));

            check(runtime.Contains("for (int playerId = 1; playerId <= 8; playerId++)") &&
                  runtime.Contains("CaptureKickedPlayerIds()") &&
                  !runtime.Contains("ActivePlayerHelper"),
                "Lord fix scans native records 1-8 and treats the roster only as a kicked veto");
            check(runtime.Contains("GameTimeManagerAPI.Instance.OnTick += OnGameTick") &&
                  runtime.Contains("Shared.MissionEvents.Initialization.Subscribe(OnInitialization)") &&
                  runtime.Contains("MissionInitializationPhase.AfterNativeStart") &&
                  runtime.Contains("MissionInitializationPhase.NativeLoaded"),
                "Lord fix uses simulation ticks and both supported native initialization phases");
            check(runtime.Contains("notification.Context.StartKind == MissionStartKind.NewGame") &&
                  !runtime.Contains("== Shared.GameModeKind") &&
                  !runtime.Contains("== GameModeKind"),
                "Lord fix is bounded by NewGame lifecycle rather than game-mode classification");
            int globalSetter = runtime.IndexOf("SetLordUnitGlobalId(playerId, 0)", StringComparison.Ordinal);
            int unitSetter = runtime.IndexOf("SetLordUnitId(playerId, 0)", StringComparison.Ordinal);
            check(globalSetter >= 0 && unitSetter > globalSetter &&
                  runtime.Contains("snapshot.LordIdentity.Type == eChimps.CHIMP_TYPE_NULL") &&
                  runtime.Contains("snapshot.LordIdentity.AliveState == AliveState.None") &&
                  !runtime.Contains("SetWinLossState"),
                "Lord fix keeps the exact tombstone guard and clears global ID before unit ID");
            check(runtime.Contains("0xC6810") && runtime.Contains("0xC23C0") && runtime.Contains("0x17FEF0") &&
                  runtime.Contains("version-sensitive displaced-code") &&
                  runtime.Contains("gameMembers is populated by only some launch paths"),
                "Lord fix documents the Vanilla cause and managed-workaround tradeoff");
            check(!runtime.Contains("AddDetour") && !runtime.Contains("AddContextHook") &&
                  !runtime.Contains("CodePatch") && !runtime.Contains("OnDestroy") &&
                  !runtime.Contains("OnDisable") && !runtime.Contains("OnApplicationQuit") &&
                  !runtime.Contains("public void Dispose"),
                "Lord fix adds neither native hooks nor teardown paths");
            check(policy.Contains("if (!input.Enabled)") &&
                  viewModel.Contains("private bool enableCorruptLordDataSpawnFix = true;") &&
                  viewModel.Contains("[SyncHostOnly]" + Environment.NewLine +
                    "        public bool EnableCorruptLordDataSpawnFix") &&
                  viewModel.Contains("EnableCorruptLordDataSpawnFix = true;"),
                "Lord fix is host-synchronized and enabled by default and reset");
            check(orchestrator.Contains("private static CorruptLordDataSpawnRuntime processCorruptLordDataSpawnRuntime;") &&
                  orchestrator.Contains("candidate.Install();" + Environment.NewLine +
                    "            processCorruptLordDataSpawnRuntime = candidate;"),
                "Lord fix runtime is statically rooted only after successful installation");
            check(xaml.Contains("IsChecked=\"{Binding EnableCorruptLordDataSpawnFix, Mode=TwoWay}\"") &&
                  xaml.Contains("bugfixes.enable-corrupt-lord-data-spawn-fix"),
                "Lord fix host setting is exposed in Gameplay fixes");

            string[] localeFiles = Directory.GetFiles("Locales", "*.txt");
            check(localeFiles.Length == 21 && localeFiles.All(path =>
                File.ReadAllText(path).Contains("BugfixesAndQoL.EnableCorruptLordDataSpawnFix=") &&
                File.ReadAllText(path).Contains("BugfixesAndQoL.EnableCorruptLordDataSpawnFixHelp=")),
                "Lord fix label and help exist in all 21 locales");
        }

        private static CorruptLordDataSpawnDecision Evaluate(
            bool enabled = true,
            bool isNewGameSession = true,
            bool correctionWindowOpen = true,
            bool hasPlayerRecord = true,
            bool kicked = false,
            bool alreadyAttempted = false,
            bool isDefeated = false,
            int lordUnitId = 8,
            int lordGlobalId = 534,
            bool lordUnitResolved = true,
            int lordOwnerPlayerId = 0,
            bool lordTypeIsNull = true,
            bool lordAliveStateIsNone = true,
            int lordUnitGlobalId = 0,
            int lordCurrentHealth = 0,
            bool validOwnedKeep = true,
            bool validOwnedKeepDoorReference = true)
        {
            var input = new CorruptLordDataSpawnGuardInput(
                enabled, isNewGameSession, correctionWindowOpen, hasPlayerRecord,
                kicked, alreadyAttempted, isDefeated, lordUnitId, lordGlobalId,
                lordUnitResolved, lordOwnerPlayerId, lordTypeIsNull,
                lordAliveStateIsNone, lordUnitGlobalId, lordCurrentHealth,
                validOwnedKeep, validOwnedKeepDoorReference);
            return CorruptLordDataSpawnPolicy.Evaluate(in input);
        }
    }
}
