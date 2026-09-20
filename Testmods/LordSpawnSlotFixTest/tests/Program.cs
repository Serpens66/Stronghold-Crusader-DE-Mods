using LordSpawnSlotFixTest;
using System;
using System.IO;

namespace LordSpawnSlotFixTest.Tests
{
    internal static class Program
    {
        private static int checks;

        private static int Main()
        {
            try
            {
                CheckPositiveDecision();
                CheckEveryGuard();
                CheckValidLordIsNeverCleared();
                CheckOneAttemptPerSession();
                CheckSessionReset();
                CheckRuntimeContract();
                Console.WriteLine($"LordSpawnSlotFixTest tests passed: {checks} checks.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static void CheckPositiveDecision()
        {
            Check(Evaluate() == LordSpawnSlotDecision.ClearStaleLordReference, "positive guard was rejected");
        }

        private static void CheckEveryGuard()
        {
            Check(Evaluate(sessionEligible: false) == LordSpawnSlotDecision.RejectIneligibleSession, "session guard");
            Check(Evaluate(correctionWindowOpen: false) == LordSpawnSlotDecision.RejectOutsideCorrectionWindow, "correction-window guard");
            Check(Evaluate(hasPlayerRecord: false) == LordSpawnSlotDecision.RejectMissingPlayerRecord, "resource guard");
            Check(Evaluate(inRoster: false) == LordSpawnSlotDecision.RejectNotInRoster, "roster guard");
            Check(Evaluate(kicked: true) == LordSpawnSlotDecision.RejectKicked, "kick guard");
            Check(Evaluate(alreadyAttempted: true) == LordSpawnSlotDecision.RejectAlreadyAttempted, "attempt guard");
            Check(Evaluate(isDefeated: true) == LordSpawnSlotDecision.RejectDefeated, "defeat guard");
            Check(Evaluate(validOwnedKeep: false) == LordSpawnSlotDecision.RejectInvalidKeep, "Keep guard");
            Check(Evaluate(validOwnedKeepDoorReference: false) == LordSpawnSlotDecision.RejectInvalidKeepDoorReference, "Keep-door-reference guard");
            Check(Evaluate(lordUnitId: 0) == LordSpawnSlotDecision.RejectMissingStoredLordUnitId, "stored Lord unit-ID guard");
            Check(Evaluate(lordGlobalId: 0) == LordSpawnSlotDecision.RejectMissingStoredLordGlobalId, "stored Lord global-ID guard");
            Check(Evaluate(lordUnitResolved: false) == LordSpawnSlotDecision.RejectLordUnitUnresolved, "resolved-unit guard");
            Check(Evaluate(lordOwnerPlayerId: 7) == LordSpawnSlotDecision.RejectLordOwnerNotZero, "zero-owner guard");
            Check(Evaluate(lordTypeIsNull: false) == LordSpawnSlotDecision.RejectLordTypeNotNull, "null-type guard");
            Check(Evaluate(lordAliveStateIsNone: false) == LordSpawnSlotDecision.RejectLordAliveStateNotNone, "none-alive-state guard");
            Check(Evaluate(lordUnitGlobalId: 517) == LordSpawnSlotDecision.RejectLordUnitGlobalIdNotZero, "zero-unit-global-ID guard");
            Check(Evaluate(lordCurrentHealth: 1) == LordSpawnSlotDecision.RejectLordHealthNotZero, "zero-health guard");
        }

        private static void CheckValidLordIsNeverCleared()
        {
            LordSpawnSlotDecision decision = Evaluate(
                lordOwnerPlayerId: 7,
                lordTypeIsNull: false,
                lordAliveStateIsNone: false,
                lordUnitGlobalId: 534,
                lordCurrentHealth: 150000);
            Check(decision != LordSpawnSlotDecision.ClearStaleLordReference, "valid Lord identity was accepted for clearing");
        }

        private static void CheckOneAttemptPerSession()
        {
            var state = new LordSpawnSlotSessionState();
            state.Reset(10);
            Check(state.MarkAttempted(7), "first P7 attempt rejected");
            Check(!state.MarkAttempted(7), "duplicate P7 attempt accepted");
            Check(state.WasAttempted(7), "P7 attempt not retained");
            Check(state.MarkConfirmed(7), "first P7 confirmation rejected");
            Check(!state.MarkConfirmed(7), "duplicate P7 confirmation accepted");
            Check(state.IsConfirmed(7), "P7 confirmation not retained");
        }

        private static void CheckSessionReset()
        {
            var state = new LordSpawnSlotSessionState();
            state.Reset(10);
            state.MarkAttempted(8);
            state.MarkConfirmed(8);
            state.Reset(11);
            Check(state.SessionId == 11, "session ID was not replaced");
            Check(!state.WasAttempted(8), "attempt leaked across sessions");
            Check(!state.IsConfirmed(8), "confirmation leaked across sessions");
        }

        private static void CheckRuntimeContract()
        {
            string runtime = File.ReadAllText(Path.Combine("src", "LordSpawnSlotFixTestRuntime.cs"));
            string plugin = File.ReadAllText(Path.Combine("src", "LordSpawnSlotFixTestPlugin.cs"));
            Check(runtime.Contains("Platform_Multiplayer.Instance?.gameMembers?.ToArray()"), "raw roster is not used");
            Check(!runtime.Contains("ActivePlayerHelper"), "loss-filtering ActivePlayerHelper is used");
            Check(runtime.Contains("GameTimeManagerAPI.Instance.OnTick += OnGameTick"), "simulation tick is not used");
            Check(runtime.Contains("SetLordUnitGlobalId(member.PlayerId, 0)"), "public global Lord identity setter is not used");
            Check(runtime.Contains("SetLordUnitId(member.PlayerId, 0)"), "public Lord unit identity setter is not used");
            Check(runtime.Contains("snapshot.LordIdentity.Type == eChimps.CHIMP_TYPE_NULL"), "exact null-type tombstone guard is absent");
            Check(runtime.Contains("snapshot.LordIdentity.AliveState == AliveState.None"), "exact none-state tombstone guard is absent");
            Check(!runtime.Contains("SetWinLossState"), "disproved WinLoss correction remains in runtime");
            Check(!runtime.Contains("AddDetour") && !runtime.Contains("AddContextHook") && !runtime.Contains("CodePatch"), "native hook surface added");
            Check(!runtime.Contains("OnDestroy") && !runtime.Contains("OnDisable") && !runtime.Contains("OnApplicationQuit"), "Unity teardown path added");
            Check(plugin.Contains("private static LordSpawnSlotFixTestRuntime persistentRuntime"), "runtime is not statically rooted");
        }

        private static LordSpawnSlotDecision Evaluate(
            bool sessionEligible = true,
            bool correctionWindowOpen = true,
            bool hasPlayerRecord = true,
            bool inRoster = true,
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
            var input = new LordSpawnSlotGuardInput(
                sessionEligible, correctionWindowOpen, hasPlayerRecord, inRoster, kicked,
                alreadyAttempted, isDefeated, lordUnitId,
                lordGlobalId, lordUnitResolved, lordOwnerPlayerId,
                lordTypeIsNull, lordAliveStateIsNone, lordUnitGlobalId,
                lordCurrentHealth, validOwnedKeep, validOwnedKeepDoorReference);
            return LordSpawnSlotFixPolicy.Evaluate(in input);
        }

        private static void Check(bool condition, string message)
        {
            checks++;
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
