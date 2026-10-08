using APIShared;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace APISharedTests
{
    internal static class PlayerDefeatTests
    {
        internal static void Run()
        {
            TestLordStateMachine();
            TestOfficialDefeatStateMachine();
            TestSessionBaselines();
            TestObserverContract();
        }

        private static void TestLordStateMachine()
        {
            var state = new PlayerDefeatState();
            var lordDeaths = new List<PlayerLordDeathNotification>();
            state.SynchronizeSession(11);
            state.Observe(1, Slots(Slot(WinLossState.None)), lordDeaths.Add, null);
            state.Observe(2, Slots(Living(4, 40)), lordDeaths.Add, null);
            state.Observe(3, Slots(Living(5, 50)), lordDeaths.Add, null);
            Check(lordDeaths.Count == 0, "a living lord replacement does not report a death");
            state.Observe(4, Slots(Slot(WinLossState.None)), lordDeaths.Add, null);
            state.Observe(5, Slots(Slot(WinLossState.None)), lordDeaths.Add, null);
            state.Observe(6, Slots(Living(6, 60)), lordDeaths.Add, null);
            state.Observe(7, Slots(Slot(WinLossState.None)), lordDeaths.Add, null);
            Check(lordDeaths.Count == 1 &&
                  lordDeaths[0].SessionId == 11 &&
                  lordDeaths[0].PlayerId == 1 &&
                  lordDeaths[0].LordUnitId == 5 &&
                  lordDeaths[0].LordGlobalId == 50 &&
                  lordDeaths[0].SimulationTick == 4,
                "lord death is armed by life, follows replacement identity and publishes once");

            var delayedSpawn = new PlayerDefeatState();
            lordDeaths.Clear();
            delayedSpawn.SynchronizeSession(12);
            delayedSpawn.Observe(1, Slots(Slot(WinLossState.None)), lordDeaths.Add, null);
            delayedSpawn.Observe(2, Slots(Living(7, 70)), lordDeaths.Add, null);
            delayedSpawn.Observe(3, Slots(Slot(WinLossState.None)), lordDeaths.Add, null);
            Check(lordDeaths.Count == 1 && lordDeaths[0].LordUnitId == 7,
                "a lord spawned after the baseline arms the detector");
        }

        private static void TestOfficialDefeatStateMachine()
        {
            var state = new PlayerDefeatState();
            var defeats = new List<PlayerDefeatNotification>();
            state.SynchronizeSession(21);
            state.Observe(1, Slots(Slot(WinLossState.None), Slot(WinLossState.Win)), null, defeats.Add);
            state.Observe(2, Slots(Slot(WinLossState.Loss), Slot(WinLossState.Loss)), null, defeats.Add);
            state.Observe(3, Slots(Slot(WinLossState.Loss), Slot(WinLossState.Loss)), null, defeats.Add);
            state.Observe(4, Slots(Slot(WinLossState.None), Slot(WinLossState.Win)), null, defeats.Add);
            state.Observe(5, Slots(Slot(WinLossState.Loss), Slot(WinLossState.Loss)), null, defeats.Add);
            Check(defeats.Count == 2 &&
                  defeats[0].PlayerId == 1 &&
                  defeats[1].PlayerId == 2 &&
                  defeats[0].SimulationTick == 2 &&
                  defeats[1].SimulationTick == 2,
                "None-to-Loss and Win-to-Loss publish once per player and session");

            var lordless = new PlayerDefeatState();
            defeats.Clear();
            lordless.SynchronizeSession(22);
            lordless.Observe(1, Slots(Slot(WinLossState.None)), null, defeats.Add);
            lordless.Observe(2, Slots(Slot(WinLossState.Loss)), null, defeats.Add);
            Check(defeats.Count == 1,
                "official defeat does not depend on a lord identity");
        }

        private static void TestSessionBaselines()
        {
            var state = new PlayerDefeatState();
            var lordDeaths = new List<PlayerLordDeathNotification>();
            var defeats = new List<PlayerDefeatNotification>();
            state.SynchronizeSession(31);
            state.Observe(8, Slots(Slot(WinLossState.Loss)), lordDeaths.Add, defeats.Add);
            state.Observe(9, Slots(Slot(WinLossState.Loss)), lordDeaths.Add, defeats.Add);
            Check(lordDeaths.Count == 0 && defeats.Count == 0,
                "a loaded save with an existing death or loss only establishes a baseline");

            state.Observe(10, Slots(Living(8, 80)), lordDeaths.Add, defeats.Add);
            state.SynchronizeSession(null);
            state.SynchronizeSession(32);
            state.Observe(1, Slots(Slot(WinLossState.Loss)), lordDeaths.Add, defeats.Add);
            Check(lordDeaths.Count == 0 && defeats.Count == 0,
                "mission end and session change clear armed and official state without replay");
        }

        private static void TestObserverContract()
        {
            Check(!ApiShared.Current.TryGetPlayerDefeat(
                    "",
                    out _,
                    out NativeCapabilityDiagnostic invalidOwner) &&
                  invalidOwner.State == NativeCapabilityState.ValidationFailed,
                "capability acquisition rejects an empty owner GUID");
            PlayerDefeatService service = PlayerDefeatService.TestCreate();
            IPlayerDefeatCapability beta = service.Bind("beta");
            IPlayerDefeatCapability alpha = service.Bind("alpha");
            var calls = new List<string>();

            Check(alpha.TryRegisterObserver("two", _ => calls.Add("alpha/two/lord"),
                    _ => calls.Add("alpha/two/defeat"), out NativeCapabilityDiagnostic available) &&
                  available.State == NativeCapabilityState.Available,
                "valid observer registration is available");
            Check(beta.TryRegisterObserver("one", _ => calls.Add("beta/one/lord"),
                    _ => calls.Add("beta/one/defeat"), out _),
                "second owner registration succeeds");
            Check(!alpha.TryRegisterObserver("two", _ => { }, null, out NativeCapabilityDiagnostic duplicate) &&
                  duplicate.State == NativeCapabilityState.Conflict,
                "duplicate owner-local registration IDs are rejected");
            Check(!alpha.TryRegisterObserver("", _ => { }, null, out _) &&
                  !alpha.TryRegisterObserver("empty", null, null, out _),
                "invalid registration IDs and empty callback sets are rejected");

            IPlayerDefeatCapability first = service.Bind("00-throwing");
            Check(first.TryRegisterObserver(
                    "observer",
                    _ =>
                    {
                        calls.Add("00/lord");
                        service.TestPublish(new PlayerDefeatNotification(41, 1, 9));
                        throw new InvalidOperationException("expected test exception");
                    },
                    _ => calls.Add("00/defeat"),
                    out _),
                "throwing observer registers");

            service.TestPublish(new PlayerLordDeathNotification(41, 1, 2, 20, 8));
            string joined = string.Join(",", calls);
            Check(joined ==
                  "00/lord,alpha/two/lord,beta/one/lord,00/defeat,alpha/two/defeat,beta/one/defeat",
                "observers are deterministic, reentrant publication is queued and exceptions are isolated");
            Check(NativeCapabilityIds.PlayerDefeat == "player-defeat",
                "player-defeat capability ID is stable");
        }

        private static PlayerDefeatObservation Living(int unitId, int globalId) =>
            new PlayerDefeatObservation(true, WinLossState.None, true, unitId, globalId);

        private static PlayerDefeatObservation Slot(WinLossState state) =>
            new PlayerDefeatObservation(true, state, false, 0, 0);

        private static PlayerDefeatObservation[] Slots(params PlayerDefeatObservation[] values)
        {
            var result = new PlayerDefeatObservation[8];
            for (int index = 0; index < result.Length; index++)
                result[index] = index < values.Length
                    ? values[index]
                    : new PlayerDefeatObservation(false, WinLossState.None, false, 0, 0);
            return result;
        }

        private static void Check(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("PlayerDefeatTests failed: " + message);
        }
    }
}
