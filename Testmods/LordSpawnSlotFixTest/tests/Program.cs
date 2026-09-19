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
            Check(Evaluate() == LordSpawnSlotDecision.Correct, "positive guard was rejected");
        }

        private static void CheckEveryGuard()
        {
            Check(Evaluate(sessionEligible: false) == LordSpawnSlotDecision.RejectIneligibleSession, "session guard");
            Check(Evaluate(hasPlayerRecord: false) == LordSpawnSlotDecision.RejectMissingPlayerRecord, "resource guard");
            Check(Evaluate(inRoster: false) == LordSpawnSlotDecision.RejectNotInRoster, "roster guard");
            Check(Evaluate(kicked: true) == LordSpawnSlotDecision.RejectKicked, "kick guard");
            Check(Evaluate(alreadyAttempted: true) == LordSpawnSlotDecision.RejectAlreadyAttempted, "attempt guard");
            Check(Evaluate(isDefeated: false) == LordSpawnSlotDecision.RejectNotDefeated, "defeat guard");
            Check(Evaluate(lordUnitId: 123) == LordSpawnSlotDecision.RejectLordPresent, "Lord guard");
            Check(Evaluate(validOwnedKeep: false) == LordSpawnSlotDecision.RejectInvalidKeep, "Keep guard");
            Check(Evaluate(validOwnedKeepDoor: false) == LordSpawnSlotDecision.RejectInvalidKeepDoor, "Keep-door guard");
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
            Check(runtime.Contains("SetWinLossState(member.PlayerId, WinLossState.None)"), "public correction API is not used");
            Check(!runtime.Contains("AddDetour") && !runtime.Contains("AddContextHook") && !runtime.Contains("CodePatch"), "native hook surface added");
            Check(!runtime.Contains("OnDestroy") && !runtime.Contains("OnDisable") && !runtime.Contains("OnApplicationQuit"), "Unity teardown path added");
            Check(plugin.Contains("private static LordSpawnSlotFixTestRuntime persistentRuntime"), "runtime is not statically rooted");
        }

        private static LordSpawnSlotDecision Evaluate(
            bool sessionEligible = true,
            bool hasPlayerRecord = true,
            bool inRoster = true,
            bool kicked = false,
            bool alreadyAttempted = false,
            bool isDefeated = true,
            int lordUnitId = 0,
            bool validOwnedKeep = true,
            bool validOwnedKeepDoor = true)
        {
            var input = new LordSpawnSlotGuardInput(
                sessionEligible, hasPlayerRecord, inRoster, kicked,
                alreadyAttempted, isDefeated, lordUnitId,
                validOwnedKeep, validOwnedKeepDoor);
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
