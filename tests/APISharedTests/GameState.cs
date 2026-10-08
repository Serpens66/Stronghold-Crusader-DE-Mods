using APIShared.ModSettings;
using APIShared;
using CrusaderDE;
using Iced.Intel;
using APIShared.Internal;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace APISharedTests
{
    public partial class RuntimeTests
    {
        [TestMethod]
        [TestCategory("GameState")]
        public void VerifyPlayerPerspectivePolicy() => TestPlayerPerspectivePolicy();

        private static void TestPlayerPerspectivePolicy()
        {
            int multiplayer = (int)SHCDESE.Interop.Enums.eGameTypeModes.GAMETYPE_MULTIPLAYER;
            Assert(PlayerPerspectiveAPI.ShouldOverrideLocalPlayerId(1, multiplayer, 1, -1),
                "Original spectators with a selected view must have no controlled player.");
            Assert(PlayerPerspectiveAPI.ShouldOverrideLocalPlayerId(8, multiplayer, 1, 0),
                "The last occupied spectator slot must be supported.");
            Assert(!PlayerPerspectiveAPI.ShouldOverrideLocalPlayerId(1, multiplayer, 1, 1),
                "Eliminated players retain their controlled identity.");
            Assert(!PlayerPerspectiveAPI.ShouldOverrideLocalPlayerId(0, multiplayer, 1, -1),
                "A cleared view must not override the Script Extender result.");
            Assert(!PlayerPerspectiveAPI.ShouldOverrideLocalPlayerId(1, multiplayer, 0, -1),
                "Non-spectator matches must keep the original result.");
            Assert(!PlayerPerspectiveAPI.ShouldOverrideLocalPlayerId(1,
                (int)SHCDESE.Interop.Enums.eGameTypeModes.GAMETYPE_MAP, 1, -1),
                "Other game modes must keep the original result.");
        }
        [TestMethod]
        [TestCategory("GameState")]
        public void VerifyPlayerPerspectiveTransition() => TestPlayerPerspectiveTransition();

        private static void TestPlayerPerspectiveTransition()
        {
            FieldInfo activeField = typeof(PlayerPerspectiveAPI).GetField("identityOverrideActive",
                BindingFlags.Static | BindingFlags.NonPublic);
            FieldInfo selectedField = typeof(PlayerPerspectiveAPI).GetField("selectedSpectatorPlayerId",
                BindingFlags.Static | BindingFlags.NonPublic);
            int Active() => (int)activeField.GetValue(null);
            int Selected() => (int)selectedField.GetValue(null);
            PlayerPerspectiveAPI.ClearSpectatorView();
            int native = 1;
            bool protectedDuringWrite = false;
            int viewDuringWrite = 0;
            bool changed = PlayerPerspectiveAPI.TryChangeView(2, () => native, value =>
            {
                protectedDuringWrite = Active() == 1 && Selected() == 0;
                native = value;
                viewDuringWrite = PlayerPerspectiveAPI.GetViewedPlayerId();
            }, out string failure);
            Assert(changed && failure == null && protectedDuringWrite && native == 2 &&
                Active() == 1 && Selected() == 2 && viewDuringWrite == 1 &&
                PlayerPerspectiveAPI.GetViewedPlayerId() == 2,
                "spectator identity is protected and the new viewpoint published only after verification");

            int writes = 0;
            changed = PlayerPerspectiveAPI.TryChangeView(3, () => native, value =>
            {
                writes++;
                native = writes == 1 ? 4 : value;
            }, out failure);
            Assert(!changed && writes == 2 && native == 2 && Active() == 1 && Selected() == 2 &&
                PlayerPerspectiveAPI.GetViewedPlayerId() == 2 && failure.Contains("rollback=verified"),
                "a failed switch restores the previous native and published viewpoint");

            PlayerPerspectiveAPI.ClearSpectatorView();
            native = 1;
            writes = 0;
            changed = PlayerPerspectiveAPI.TryChangeView(3, () => native, value =>
            {
                writes++;
                native = writes == 1 ? 4 : value;
            }, out failure);
            Assert(!changed && native == 1 && Active() == 0 && Selected() == 0,
                "a failed initial switch clears the override after verified rollback");

            writes = 0;
            changed = PlayerPerspectiveAPI.TryChangeView(3, () => native, value =>
            {
                if (++writes == 2) throw new InvalidOperationException("rollback unavailable");
                native = 4;
            }, out failure);
            Assert(!changed && native == 4 && Active() == 1 && Selected() == 0 &&
                PlayerPerspectiveAPI.GetViewedPlayerId() == -1 && failure.Contains("rollback=failed"),
                "failed rollback keeps the spectator identity protected and does not publish an unverified view");
            PlayerPerspectiveAPI.ClearSpectatorView();
        }
        [TestMethod]
        [TestCategory("GameState")]
        public void VerifyElevatedMoatAiState() => TestElevatedMoatAiState();

        private static void TestElevatedMoatAiState()
        {
            int changes = 0;
            Action<ElevatedMoatAiState> observer = _ => changes++;
            ElevatedMoatAiCapability.Changed += observer;
            Assert(ElevatedMoatAiCapability.Current == ElevatedMoatAiState.Unknown,
                "elevated AI construction starts unknown");
            ElevatedMoatAiCapability.Publish(ElevatedMoatAiState.Enabled);
            ElevatedMoatAiCapability.Publish(ElevatedMoatAiState.Enabled);
            Assert(ElevatedMoatAiCapability.Current == ElevatedMoatAiState.Enabled && changes == 1,
                "effective enabled state is published once");
            ElevatedMoatAiCapability.Publish(ElevatedMoatAiState.Disabled);
            Assert(ElevatedMoatAiCapability.Current == ElevatedMoatAiState.Disabled && changes == 2,
                "effective disabled state reaches consumers");
            ElevatedMoatAiCapability.Changed -= observer;
            ElevatedMoatAiCapability.Publish(ElevatedMoatAiState.Unknown);
        }
        [TestMethod]
        [TestCategory("GameState")]
        public void VerifyLobbyStateCapability() => TestLobbyStateCapability();

        private static void TestLobbyStateCapability()
        {
            var source = new Dictionary<int, ulong> { [1] = 1001UL };
            var first = new LobbyStateSnapshot(
                42UL, source, false, 1, false, string.Empty, string.Empty);
            source[1] = 9001UL;
            Assert(first.Players[1] == 1001UL,
                "lobby snapshots must copy their player map");
            AssertThrows<NotSupportedException>(
                () => ((IDictionary<int, ulong>)first.Players)[2] = 1002UL,
                "lobby snapshot player maps must be immutable");

            var equal = new LobbyStateSnapshot(
                42UL,
                new Dictionary<int, ulong> { [1] = 1001UL },
                false,
                1,
                false,
                string.Empty,
                string.Empty);
            var changed = new LobbyStateSnapshot(
                42UL,
                new Dictionary<int, ulong> { [1] = 1001UL, [2] = 1002UL },
                false,
                1,
                false,
                string.Empty,
                string.Empty);
            Assert(LobbyStateService.ValueEquals(first, equal) &&
                !LobbyStateService.ValueEquals(first, changed),
                "lobby snapshot equality must compare every published value");
            Assert(LobbyStateService.ShouldObserve(false, true, 10, 11) &&
                !LobbyStateService.ShouldObserve(false, false, 10, 24) &&
                LobbyStateService.ShouldObserve(false, false, 10, 25) &&
                !LobbyStateService.ShouldObserve(true, true, 10, 25) &&
                LobbyStateService.ShouldObserve(false, true, 25, 26),
                "lobby polling must honor dirty, 15-frame fallback, map suppression and dirty resume");

            var service = new LobbyStateService(null);
            var calls = new List<string>();
            ILobbyStateCapability zOwner = service.Bind("z.owner");
            ILobbyStateCapability aOwner = service.Bind("a.owner");
            Assert(zOwner.TryRegisterObserver("one", _ => calls.Add("z"), out _),
                "first lobby observer registration should succeed");
            Assert(aOwner.TryRegisterObserver("two", snapshot =>
            {
                calls.Add("a2:" + snapshot.Players.Count);
                if (snapshot.Players.Count == 1)
                    service.System_TestPublish(changed);
            }, out _), "second lobby observer registration should succeed");
            Assert(aOwner.TryRegisterObserver("one", _ => calls.Add("a1"), out _),
                "third lobby observer registration should succeed");
            Assert(!aOwner.TryRegisterObserver("one", _ => { }, out NativeCapabilityDiagnostic duplicate) &&
                duplicate.State == NativeCapabilityState.ValidationFailed,
                "duplicate lobby observer IDs must fail closed");

            service.System_TestPublish(first);
            Assert(string.Join(",", calls) == "a1,a2:1,z,a1,a2:2,z",
                "lobby observers must be ordered and nested publication must unwind deterministically");
            calls.Clear();
            service.System_TestPublish(new LobbyStateSnapshot(
                42UL,
                new Dictionary<int, ulong> { [1] = 1001UL, [2] = 1002UL },
                false,
                1,
                false,
                string.Empty,
                string.Empty));
            Assert(calls.Count == 0,
                "equal lobby snapshots must not be republished");
            Assert(aOwner.TryRegisterObserver("late", _ => calls.Add("late"), out _ ) &&
                string.Join(",", calls) == "late",
                "late lobby observers must receive the current snapshot synchronously");

            var isolated = new LobbyStateService(null);
            var isolatedCalls = new List<string>();
            isolated.Bind("a").TryRegisterObserver("throws", _ =>
                throw new InvalidOperationException("expected"), out _);
            isolated.Bind("b").TryRegisterObserver("continues", _ =>
                isolatedCalls.Add("continues"), out _);
            isolated.System_TestPublish(first);
            Assert(isolatedCalls.Count == 1,
                "one failing lobby observer must not stop later observers");

            var lifecycle = new LobbyStateService(null);
            var lifecycleStates = new List<string>();
            lifecycle.Bind("owner").TryRegisterObserver("lifecycle", snapshot =>
                lifecycleStates.Add(
                    snapshot.Error.Length != 0 ? "error" :
                    !snapshot.LobbyId.HasValue ? "left" :
                    snapshot.HasUnresolvedPlayers ? "unresolved" :
                    "lobby:" + snapshot.Players.Count), out _);
            lifecycle.System_TestPublish(new LobbyStateSnapshot(
                null, null, false, 0, false, string.Empty, string.Empty));
            lifecycle.System_TestPublish(first);
            lifecycle.System_TestPublish(new LobbyStateSnapshot(
                42UL,
                new Dictionary<int, ulong> { [1] = 1001UL, [2] = 1002UL },
                true,
                1,
                false,
                string.Empty,
                string.Empty));
            lifecycle.System_TestPublish(new LobbyStateSnapshot(
                null, null, true, 0, false, "capture failed", "details"));
            lifecycle.System_TestPublish(changed);
            lifecycle.System_TestPublish(new LobbyStateSnapshot(
                null, null, false, 0, false, string.Empty, string.Empty));
            Assert(string.Join(",", lifecycleStates) ==
                "left,lobby:1,unresolved,error,lobby:2,left",
                "lobby snapshots must publish join, membership, unresolved, error, recovery and leave transitions");
        }

    }
}
