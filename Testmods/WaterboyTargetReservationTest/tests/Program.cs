using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace WaterboyTargetReservationTest
{
    internal static class Program
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int TargetSearchProbeDelegate(IntPtr buildingManager, int unitId);

        private const string DllPath =
            @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll";
        private const string ExtenderDir =
            @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins\000shcdese";
        private static int assertions;

        private static int Main()
        {
            TestIndependentReservationsAndCapacity();
            TestCompoundCoverage();
            TestInvalidationPaths();
            TestStationaryTimeoutAndSuppression();
            TestLongRouteAndTickReset();
            TestExtinguishingStateDoesNotTimeout();
            TestNearestTakeoverPolicy();
            TestPerPlayerIsolationAndTransfer();
            TestPerPlayerModeState();
            TestMapClear();
            TestManagedContracts();
            TestCanonicalNativeContract();
            TestInstalledRedBirdDisplacedSpan();
            Console.WriteLine($"WaterboyTargetReservationTest tests: {assertions} assertions passed.");
            return 0;
        }

        private static void TestIndependentReservationsAndCapacity()
        {
            var ledger = new ReservationLedger();
            NativeIdentity owner1 = Id(1, 101);
            NativeIdentity owner2 = Id(2, 102);
            NativeIdentity fire1 = Id(11, 201);
            NativeIdentity fire2 = Id(12, 202);
            Check(ledger.Claim(1, owner1, fire1, 0, 5, 5, 0) == ReservationClaimResult.Claimed,
                "first independent fire can be claimed");
            Check(ledger.TryGetCoveringReservation(1, fire1, 0, owner2.GlobalId, out _),
                "claimed fire is hidden from another waterboy");
            Check(!ledger.TryGetCoveringReservation(1, fire2, 0, owner2.GlobalId, out _),
                "independent fire remains visible");
            Check(ledger.Claim(1, owner2, fire2, 0, 6, 6, 0) == ReservationClaimResult.Claimed,
                "second waterboy claims a different fire");
            Check(ledger.Count == 2, "two independent reservations coexist");

            var excessLedger = new ReservationLedger();
            Check(excessLedger.Claim(1, owner1, fire1, 0, 5, 5, 0) == ReservationClaimResult.Claimed,
                "single fire claimed");
            Check(excessLedger.Claim(1, owner2, fire1, 0, 6, 6, 0) == ReservationClaimResult.Conflict,
                "excess waterboy cannot claim the same fire");
        }

        private static void TestCompoundCoverage()
        {
            var ledger = new ReservationLedger();
            NativeIdentity owner1 = Id(1, 101);
            NativeIdentity owner2 = Id(2, 102);
            NativeIdentity part1 = Id(20, 301);
            NativeIdentity part2 = Id(21, 302);
            Check(ledger.Claim(1, owner1, part1, 9001, 4, 4, 0) == ReservationClaimResult.Claimed,
                "compound root claimed");
            Check(ledger.TryGetCoveringReservation(1, part2, 9001, owner2.GlobalId, out _),
                "other burning compound part is covered dynamically");
            Check(ledger.Claim(1, owner2, part2, 9001, 8, 8, 0) == ReservationClaimResult.Conflict,
                "compound part cannot be double-reserved");
            Check(!ledger.TryGetCoveringReservation(1, part2, 0, owner2.GlobalId, out _),
                "zero compound key does not alias a compound");
        }

        private static void TestInvalidationPaths()
        {
            CheckInvalidation(false, ReservationLedger.WalkingState, Id(11, 201), true, 0,
                "dead owner or reused owner slot releases");
            CheckInvalidation(true, 1, Id(11, 201), true, 0,
                "owner leaving target states releases");
            CheckInvalidation(true, ReservationLedger.WalkingState, Id(12, 202), true, 0,
                "target change releases");
            CheckInvalidation(true, ReservationLedger.WalkingState, Id(11, 202), true, 0,
                "reused target slot with a new global ID releases");
            CheckInvalidation(true, ReservationLedger.WalkingState, Id(11, 201), false, 0,
                "extinguished or destroyed target releases");
            CheckInvalidation(true, ReservationLedger.WalkingState, Id(11, 201), true, 99,
                "compound identity change releases");
        }

        private static void CheckInvalidation(
            bool ownerValid,
            int ownerState,
            NativeIdentity currentTarget,
            bool targetBurning,
            uint compoundKey,
            string message)
        {
            var ledger = new ReservationLedger();
            NativeIdentity owner = Id(1, 101);
            NativeIdentity target = Id(11, 201);
            ledger.Claim(1, owner, target, 0, 5, 5, 0);
            ReservationReconcileResult result = ReconcileFirst(
                ledger,
                ownerValid,
                ownerState,
                currentTarget,
                targetBurning,
                compoundKey,
                5,
                5,
                1);
            Check(result == ReservationReconcileResult.Released && ledger.Count == 0, message);
        }

        private static void TestStationaryTimeoutAndSuppression()
        {
            var ledger = new ReservationLedger();
            NativeIdentity owner = Id(1, 101);
            NativeIdentity target = Id(11, 201);
            ledger.Claim(1, owner, target, 0, 5, 5, 100);
            Check(ReconcileFirst(ledger, true, ReservationLedger.WalkingState, target, true, 0, 5, 5, 699) ==
                  ReservationReconcileResult.Kept,
                "stationary reservation remains at 599 ticks");
            Check(ReconcileFirst(ledger, true, ReservationLedger.WalkingState, target, true, 0, 5, 5, 700) ==
                  ReservationReconcileResult.Stalled,
                "stationary reservation releases at 600 ticks");
            Check(ledger.TryGetSuppressedCoverage(owner, 5, 5, out NativeIdentity suppressedTarget, out uint suppressedCompound) &&
                  suppressedTarget.Equals(target) && suppressedCompound == 0,
                "unchanged stalled assignment is exposed for pre-search masking");
            Check(ledger.Claim(1, owner, target, 0, 5, 5, 701) == ReservationClaimResult.Suppressed,
                "unchanged stalled assignment is not re-reserved");
            Check(!ledger.TryGetSuppressedCoverage(owner, 6, 5, out _, out _),
                "movement clears pre-search suppression");
            Check(ledger.Claim(1, owner, target, 0, 6, 5, 702) == ReservationClaimResult.Claimed,
                "movement clears stalled-assignment suppression");

            Check(ReconcileFirst(ledger, true, ReservationLedger.WalkingState, target, true, 0, 7, 5, 800) ==
                  ReservationReconcileResult.Kept,
                "movement refreshes progress timestamp");
            Check(ReconcileFirst(ledger, true, ReservationLedger.WalkingState, target, true, 0, 7, 5, 1_399) ==
                  ReservationReconcileResult.Kept,
                "refreshed timeout uses last movement");

            var compoundLedger = new ReservationLedger();
            NativeIdentity compoundTarget = Id(20, 301);
            NativeIdentity compoundPart = Id(21, 302);
            compoundLedger.Claim(1, owner, compoundTarget, 9001, 9, 9, 0);
            Check(ReconcileFirst(
                    compoundLedger,
                    true,
                    ReservationLedger.WalkingState,
                    compoundTarget,
                    true,
                    9001,
                    9,
                    9,
                    600) == ReservationReconcileResult.Stalled,
                "compound reservation stalls at 600 ticks");
            Check(compoundLedger.TryGetSuppressedCoverage(owner, 9, 9, out suppressedTarget, out suppressedCompound) &&
                  suppressedTarget.Equals(compoundTarget) && suppressedCompound == 9001,
                "stalled compound retains its complete masking key");
            Check(ReservationLedger.CoversSuppression(
                    compoundPart,
                    9001,
                    suppressedTarget,
                    suppressedCompound),
                "pre-search suppression covers another burning compound part");
            Check(!ReservationLedger.CoversSuppression(
                    Id(22, 303),
                    0,
                    suppressedTarget,
                    suppressedCompound),
                "pre-search suppression leaves an independent fire visible");
            Check(compoundLedger.Claim(1, owner, Id(22, 303), 0, 9, 9, 601) == ReservationClaimResult.Claimed &&
                  compoundLedger.SuppressionCount == 0,
                "selecting an alternative target clears suppression");
        }

        private static void TestLongRouteAndTickReset()
        {
            var ledger = new ReservationLedger();
            NativeIdentity owner = Id(1, 101);
            NativeIdentity target = Id(11, 201);
            ledger.Claim(1, owner, target, 0, 5, 5, 0);
            ushort x = 5;
            for (int tick = 100; tick <= 1_800; tick += 100)
            {
                x++;
                Check(ReconcileFirst(
                        ledger,
                        true,
                        ReservationLedger.WalkingState,
                        target,
                        true,
                        0,
                        x,
                        5,
                        tick) == ReservationReconcileResult.Kept,
                    "long route remains reserved while tile progress continues");
            }
            Check(ledger.Count == 1, "route length alone never expires a reservation");

            var pausedLedger = new ReservationLedger();
            pausedLedger.Claim(1, owner, target, 0, 5, 5, 50);
            for (int sample = 0; sample < 10; sample++)
            {
                Check(ReconcileFirst(
                        pausedLedger,
                        true,
                        ReservationLedger.WalkingState,
                        target,
                        true,
                        0,
                        5,
                        5,
                        50) == ReservationReconcileResult.Kept,
                    "unchanged simulation tick consumes no timeout");
            }

            var resetLedger = new ReservationLedger();
            resetLedger.Claim(1, owner, target, 0, 5, 5, 1_000);
            Check(ReconcileFirst(resetLedger, true, ReservationLedger.WalkingState, target, true, 0, 5, 5, 10) ==
                  ReservationReconcileResult.Kept,
                "tick rollback resets the progress baseline fail-safe");
            Check(ReconcileFirst(resetLedger, true, ReservationLedger.WalkingState, target, true, 0, 5, 5, 609) ==
                  ReservationReconcileResult.Kept,
                "rollback baseline remains reserved at 599 ticks");
            Check(ReconcileFirst(resetLedger, true, ReservationLedger.WalkingState, target, true, 0, 5, 5, 610) ==
                  ReservationReconcileResult.Stalled,
                "rollback baseline expires at 600 ticks");
        }

        private static void TestExtinguishingStateDoesNotTimeout()
        {
            var ledger = new ReservationLedger();
            NativeIdentity owner = Id(1, 101);
            NativeIdentity target = Id(11, 201);
            ledger.Claim(1, owner, target, 0, 5, 5, 0);
            Check(ReconcileFirst(ledger, true, ReservationLedger.ExtinguishingState, target, true, 0, 5, 5, 50_000) ==
                  ReservationReconcileResult.Kept,
                "extinguishing animation retains reservation without movement");
        }

        private static void TestNearestTakeoverPolicy()
        {
            Check(WaterboyTargetPolicy.ManhattanDistance(4, 7, 10, 3) == 10,
                "takeover distance uses Vanilla Manhattan metric");
            Check(WaterboyTargetPolicy.CanTakeOver(1, ReservationLedger.WalkingState, 4, 5),
                "strictly nearer idle state-1 waterboy may take over");
            Check(WaterboyTargetPolicy.CanTakeOver(2, ReservationLedger.WalkingState, 4, 5),
                "strictly nearer idle state-2 waterboy may take over");
            Check(!WaterboyTargetPolicy.CanTakeOver(1, ReservationLedger.WalkingState, 5, 5),
                "equal distance keeps current owner");
            Check(!WaterboyTargetPolicy.CanTakeOver(1, ReservationLedger.WalkingState, 6, 5),
                "farther requester keeps current owner");
            Check(!WaterboyTargetPolicy.CanTakeOver(ReservationLedger.WalkingState,
                    ReservationLedger.WalkingState, 1, 10),
                "walking requester cannot displace another assignment");
            Check(!WaterboyTargetPolicy.CanTakeOver(1, ReservationLedger.ExtinguishingState, 1, 10),
                "state-4 owner can never be displaced");
        }

        private static void TestPerPlayerIsolationAndTransfer()
        {
            var ledger = new ReservationLedger();
            NativeIdentity owner1 = Id(1, 101);
            NativeIdentity owner2 = Id(2, 102);
            NativeIdentity owner3 = Id(3, 103);
            NativeIdentity fire = Id(11, 201);
            Check(ledger.Claim(1, owner1, fire, 9001, 10, 10, 0) == ReservationClaimResult.Claimed,
                "player one creates reservation");
            Check(ledger.Claim(2, owner2, fire, 9001, 11, 10, 0) == ReservationClaimResult.Claimed,
                "different player may independently reserve same allied fire");
            Check(ledger.TryGetCoveringReservation(1, fire, 9001, owner3.GlobalId,
                    out FireReservation previous) && previous.Owner.Equals(owner1),
                "same-player conflict resolves to existing owner");
            Check(ledger.TryTransfer(previous, 1, owner3, fire, 9001, 4, 4, 12),
                "takeover atomically transfers same-player reservation");
            Check(!ledger.TryGetCoveringReservation(1, fire, 9001, owner3.GlobalId, out _),
                "new owner sees its own reservation as available");
            Check(ledger.TryGetCoveringReservation(1, fire, 9001, owner1.GlobalId,
                    out FireReservation transferred) && transferred.Owner.Equals(owner3),
                "old owner now conflicts with transferred reservation");
            ledger.ClearPlayer(1);
            Check(ledger.Count == 1 &&
                  ledger.TryGetCoveringReservation(2, fire, 9001, owner3.GlobalId, out _),
                "disabling one player preserves other players' reservations");
        }

        private static void TestPerPlayerModeState()
        {
            var state = new PerPlayerModeState();
            for (int playerId = 1; playerId <= 8; playerId++)
                Check(state.Data[playerId], $"player {playerId} starts optimized");
            state.Data[2] = false;
            Check(state.LocalValue && !state.Data[2],
                "remote companion slot remains separate from persisted local value");
            state.SetLocalValue(false);
            state.ResolveLocalPlayer(1);
            Check(!state.LocalValue && !state.Data[1] && !state.Data[2],
                "persisted local value populates only the resolved local slot");

            Check(!WaterboyModePolicy.Resolve(false, 1, 1, false, state.Data),
                "single-player save with persisted false uses Vanilla for new searches");
            state.SetLocalValue(true);
            Check(WaterboyModePolicy.Resolve(false, 1, 1, true, state.Data),
                "single-player save with persisted true enables reservations for new searches");
            Check(WaterboyModePolicy.Resolve(false, 3, 1, false, new bool[9]),
                "AI remains optimized independently of the local human setting");

            var multiplayer = new bool[9];
            multiplayer[1] = false;
            multiplayer[2] = true;
            multiplayer[3] = true;
            Check(!WaterboyModePolicy.Resolve(true, 1, 1, true, multiplayer),
                "multiplayer reads the final companion slot instead of the local scalar");
            Check(WaterboyModePolicy.Resolve(true, 3, 1, false, multiplayer),
                "slot shift across an intervening AI preserves the remapped remote value");
            Check(!WaterboyModePolicy.Resolve(true, 2, 1, true, new bool[9]),
                "multiplayer never substitutes the local scalar for a foreign player");
            Check(!WaterboyModePolicy.Resolve(true, 0, 1, true, multiplayer) &&
                  !WaterboyModePolicy.Resolve(true, 9, 1, true, multiplayer),
                "invalid player slots fail closed");
            Check(!WaterboyModePolicy.Resolve(false, 1, 0, true, multiplayer),
                "unresolved single-player identity remains Vanilla until ownership is known");
        }

        private static void TestMapClear()
        {
            var ledger = new ReservationLedger();
            NativeIdentity owner = Id(1, 101);
            NativeIdentity target = Id(11, 201);
            ledger.Claim(1, owner, target, 0, 5, 5, 0);
            ReconcileFirst(ledger, true, ReservationLedger.WalkingState, target, true, 0, 5, 5, 600);
            Check(ledger.SuppressionCount == 1, "test setup creates a stalled suppression");
            ledger.ClearPlayer(1);
            Check(ledger.SuppressionCount == 0,
                "per-player disable clears stalled suppressions");
            ledger.Claim(1, owner, target, 0, 5, 5, 700);
            ledger.Clear();
            Check(ledger.Count == 0 && ledger.SuppressionCount == 0,
                "map clear removes reservations and suppressions");
        }

        private static void TestManagedContracts()
        {
            Check(Marshal.SizeOf(typeof(GameBuilding)) == 0x32C, "GameBuilding size");
            Check(Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_AliveState)).ToInt32() == 0xD0,
                "building alive-state offset");
            Check(Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_GlobalId)).ToInt32() == 0xD8,
                "building global-ID offset");
            Check(Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_OnFireTicks)).ToInt32() ==
                  WaterboyNativeDefinition.ManagedBuildingFireTicksOffset,
                "managed building fire-ticks offset");
            Check(WaterboyNativeDefinition.NativeIndexedBuildingFireTicksOffset == 0x31A,
                "native manager-indexed building fire-ticks offset");
            Check(Marshal.SizeOf(typeof(GameUnit)) == 0x490, "GameUnit size");
            Check(Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_GlobalId)).ToInt32() == 0x94,
                "unit global-ID offset");
            Check(Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_ControllableForPlayerId)).ToInt32() == 0x92,
                "unit controllable-player offset");
            Check(Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_AIState)).ToInt32() == 0x2BC,
                "unit AI-state offset");
            Check(WaterboyNativeDefinition.NativeIndexedBuildingCompoundKeyOffset == 0x304,
                "native manager-indexed compound-key offset");
            Check(WaterboyNativeDefinition.ManagedBuildingCompoundKeyOffset == 0x2A8,
                "managed building compound-key offset");
            Check(WaterboyNativeDefinition.NativeIndexedBuildingCompoundKeyOffset -
                  WaterboyNativeDefinition.ManagedBuildingCompoundKeyOffset == 0x5C,
                "compound offset uses the audited manager-to-struct base translation");
            Check(WaterboyNativeDefinition.NativeIndexedBuildingFireTicksOffset -
                  WaterboyNativeDefinition.ManagedBuildingFireTicksOffset == 0x5C,
                "fire offset uses the audited manager-to-struct base translation");
            Check(WaterboyNativeDefinition.FiremanTargetSlotOffset == 0x39A, "fireman target-slot offset");
            Check(WaterboyNativeDefinition.FiremanTargetGlobalIdOffset == 0x39C,
                "fireman target-global-ID offset");
            Check(ReservationLedger.StationaryTimeoutTicks == 600,
                "stationary timeout is 15 game seconds at base game speed 40");
        }

        private static void TestCanonicalNativeContract()
        {
            byte[] file = File.ReadAllBytes(DllPath);
            Check(Hash(file) == WaterboyNativeDefinition.ReferenceSha256, "canonical DLL hash");
            PeImage image = PeImage.Load(file);
            PatternToken[] pattern = ParsePattern(WaterboyNativeDefinition.FindNearestBurningBuildingPattern);
            List<int> matches = image.FindExecutableMatches(pattern);
            Check(matches.Count == 1, "waterboy target-search AOB is unique");
            Check(matches[0] == WaterboyNativeDefinition.FindNearestBurningBuildingRva,
                "waterboy target-search AOB resolves to audited RVA");

            int[] expectedCallers = { 0x159DF8, 0x159F4C, 0x15A08B };
            List<int> callers = image.FindRelativeCallers(WaterboyNativeDefinition.FindNearestBurningBuildingRva);
            Check(callers.Count == expectedCallers.Length, "target search has exactly three direct callers");
            for (int index = 0; index < expectedCallers.Length; index++)
                Check(callers[index] == expectedCallers[index], $"target-search caller {index + 1}");

            byte[] displaced = image.ReadBytes(
                WaterboyNativeDefinition.FindNearestBurningBuildingRva,
                WaterboyNativeDefinition.FindNearestBurningBuildingDisplacedLength);
            Check(BitConverter.ToString(displaced).Replace("-", string.Empty) ==
                  "48896C24104889742418",
                "NativeDetour indirect entry displaces two complete instructions (10 bytes)");
        }

        private static void TestInstalledRedBirdDisplacedSpan()
        {
            Check(AssemblyName.GetAssemblyName(Path.Combine(ExtenderDir, "SHCDESE.dll"))
                    .Version.ToString() ==
                  WaterboyNativeDefinition.AuditedScriptExtenderAssemblyVersion,
                "installed Script Extender assembly version matches audited contract");
            Check(AssemblyName.GetAssemblyName(
                    Path.Combine(ExtenderDir, "RedBird.Backends.NativeX64.dll"))
                    .Version.ToString() ==
                  WaterboyNativeDefinition.AuditedRedBirdAssemblyVersion,
                "installed NativeDetour backend version matches audited contract");
            foreach (string assemblyName in new[]
            {
                "Microsoft.Extensions.Logging.Abstractions",
                "Iced",
                "RedBird.Abstractions",
                "RedBird.Core",
                "RedBird.Backends.NativeX64"
            })
            {
                Assembly.LoadFrom(Path.Combine(ExtenderDir, assemblyName + ".dll"));
            }

            Assembly abstractions = Assembly.LoadFrom(
                Path.Combine(ExtenderDir, "RedBird.Abstractions.dll"));
            Assembly backendAssembly = Assembly.LoadFrom(
                Path.Combine(ExtenderDir, "RedBird.Backends.NativeX64.dll"));
            Type backendType = backendAssembly.GetType(
                "RedBird.Backends.NativeX64.NativeDetourBackend",
                throwOnError: true);
            Type requestType = abstractions.GetType(
                "RedBird.Abstractions.Hooks.DetourRequest`1",
                throwOnError: true).MakeGenericType(typeof(TargetSearchProbeDelegate));
            byte[] file = File.ReadAllBytes(DllPath);
            PeImage image = PeImage.Load(file);
            byte[] prefix = image.ReadBytes(WaterboyNativeDefinition.FindNearestBurningBuildingRva, 32);
            IntPtr fixture = Marshal.AllocHGlobal(128);
            try
            {
                for (int index = 0; index < 128; index++)
                    Marshal.WriteByte(fixture, index, 0x90);
                Marshal.Copy(prefix, 0, fixture, prefix.Length);
                Marshal.WriteByte(fixture, prefix.Length, 0xC3);

                object request = Activator.CreateInstance(requestType);
                TargetSearchProbeDelegate callback = TargetSearchProbe;
                requestType.GetProperty("Name").SetValue(
                    request,
                    "WaterboyTargetReservationTest NativeDetour selector regression");
                requestType.GetProperty("TargetAddress").SetValue(
                    request,
                    unchecked((ulong)fixture.ToInt64()));
                requestType.GetProperty("Callback").SetValue(request, callback);
                MethodInfo create = null;
                foreach (MethodInfo method in backendType.GetMethods())
                {
                    if (method.Name == "CreateDetour" && method.IsGenericMethodDefinition &&
                        method.GetParameters().Length == 1)
                    {
                        create = method.MakeGenericMethod(typeof(TargetSearchProbeDelegate));
                        break;
                    }
                }
                if (create == null)
                    throw new MissingMethodException(backendType.FullName, "CreateDetour");

                object candidate = create.Invoke(
                    backendType.GetProperty("Instance").GetValue(null),
                    new[] { request });
                Type candidateType = candidate.GetType();
                try
                {
                    Check(candidateType.GetProperty("Scheme").GetValue(candidate).ToString() ==
                          "Indirect",
                        "installed NativeDetour backend selects the indirect scheme");
                    Check((int)candidateType.GetProperty("DisplacedByteCount").GetValue(candidate) ==
                          WaterboyNativeDefinition.FindNearestBurningBuildingDisplacedLength,
                        "installed NativeDetour backend displaces exactly 10 selector bytes");
                    Check(!(bool)candidateType.GetProperty("IsInstalled").GetValue(candidate),
                        "NativeDetour candidate remains unpublished before Enable");

                    candidateType.GetMethod("Enable").Invoke(candidate, null);
                    Check((bool)candidateType.GetProperty("IsInstalled").GetValue(candidate),
                        "NativeDetour candidate installs on the copied selector entry");
                    Check(Marshal.ReadByte(fixture, 0) == 0xFF &&
                          Marshal.ReadByte(fixture, 1) == 0x25 &&
                          Marshal.ReadByte(fixture, 6) == 0x90 &&
                          Marshal.ReadByte(fixture, 7) == 0x90 &&
                          Marshal.ReadByte(fixture, 8) == 0x90 &&
                          Marshal.ReadByte(fixture, 9) == 0x90,
                        "installed entry contains a six-byte indirect jump and four NOP bytes");
                    IntPtr pointerSlot = (IntPtr)candidateType.GetProperty("PointerSlot")
                        .GetValue(candidate);
                    int pointerDisplacement = Marshal.ReadInt32(IntPtr.Add(fixture, 2));
                    Check(IntPtr.Add(fixture, 6 + pointerDisplacement) == pointerSlot,
                        "entry displacement resolves to NativeDetour pointer slot");
                    IntPtr hookEntry = (IntPtr)candidateType.GetProperty("HookEntryPointAddress")
                        .GetValue(candidate);
                    Check(Marshal.ReadInt64(pointerSlot) == hookEntry.ToInt64(),
                        "NativeDetour pointer slot contains the managed hook entry");
                }
                finally
                {
                    ((IDisposable)candidate).Dispose();
                }
                GC.KeepAlive(callback);

                byte[] after = new byte[prefix.Length];
                Marshal.Copy(fixture, after, 0, after.Length);
                Check(ByteArraysEqual(prefix, after),
                    "disposing the unpublished NativeDetour candidate restores the selector bytes");
            }
            finally
            {
                Marshal.FreeHGlobal(fixture);
            }
        }

        private static int TargetSearchProbe(IntPtr buildingManager, int unitId) => 0;

        private static ReservationReconcileResult ReconcileFirst(
            ReservationLedger ledger,
            bool ownerValid,
            int state,
            NativeIdentity target,
            bool burning,
            uint compound,
            ushort x,
            ushort y,
            int currentTick)
        {
            var reservations = new List<FireReservation>();
            ledger.CopyReservationsTo(reservations);
            if (reservations.Count != 1)
                throw new InvalidOperationException("Expected exactly one reservation.");
            return ledger.Reconcile(
                reservations[0],
                ownerValid,
                1,
                state,
                target,
                burning,
                compound,
                x,
                y,
                currentTick);
        }

        private static bool ByteArraysEqual(byte[] left, byte[] right)
        {
            if (left.Length != right.Length)
                return false;
            for (int index = 0; index < left.Length; index++)
            {
                if (left[index] != right[index])
                    return false;
            }
            return true;
        }

        private static NativeIdentity Id(int slot, uint globalId) => new NativeIdentity(slot, globalId);

        private static PatternToken[] ParsePattern(string pattern)
        {
            string[] parts = pattern.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            PatternToken[] result = new PatternToken[parts.Length];
            for (int index = 0; index < parts.Length; index++)
                result[index] = parts[index] == "??" ? new PatternToken(0, true) : new PatternToken(Convert.ToByte(parts[index], 16), false);
            return result;
        }

        private static string Hash(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
        }

        private static void Check(bool condition, string message)
        {
            assertions++;
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private readonly struct PatternToken
        {
            internal PatternToken(byte value, bool wildcard)
            {
                Value = value;
                Wildcard = wildcard;
            }

            internal byte Value { get; }
            internal bool Wildcard { get; }
        }

        private sealed class PeImage
        {
            private readonly byte[] image;
            private readonly List<Section> executableSections;

            private PeImage(byte[] image, List<Section> executableSections)
            {
                this.image = image;
                this.executableSections = executableSections;
            }

            internal static PeImage Load(byte[] file)
            {
                int peOffset = BitConverter.ToInt32(file, 0x3C);
                ushort sectionCount = BitConverter.ToUInt16(file, peOffset + 6);
                ushort optionalHeaderSize = BitConverter.ToUInt16(file, peOffset + 20);
                int optionalHeader = peOffset + 24;
                byte[] image = new byte[BitConverter.ToInt32(file, optionalHeader + 56)];
                int headerSize = BitConverter.ToInt32(file, optionalHeader + 60);
                Buffer.BlockCopy(file, 0, image, 0, Math.Min(headerSize, file.Length));
                int sectionTable = optionalHeader + optionalHeaderSize;
                var executable = new List<Section>();
                for (int index = 0; index < sectionCount; index++)
                {
                    int entry = sectionTable + index * 40;
                    int virtualSize = BitConverter.ToInt32(file, entry + 8);
                    int virtualAddress = BitConverter.ToInt32(file, entry + 12);
                    int rawSize = BitConverter.ToInt32(file, entry + 16);
                    int rawOffset = BitConverter.ToInt32(file, entry + 20);
                    int copyLength = Math.Min(rawSize, Math.Min(file.Length - rawOffset, image.Length - virtualAddress));
                    if (copyLength > 0)
                        Buffer.BlockCopy(file, rawOffset, image, virtualAddress, copyLength);
                    if ((BitConverter.ToUInt32(file, entry + 36) & 0x20000000u) != 0)
                        executable.Add(new Section(virtualAddress, Math.Max(virtualSize, rawSize)));
                }
                return new PeImage(image, executable);
            }

            internal List<int> FindExecutableMatches(PatternToken[] pattern)
            {
                var matches = new List<int>();
                foreach (Section section in executableSections)
                {
                    int end = Math.Min(image.Length, section.Start + section.Length) - pattern.Length;
                    for (int offset = section.Start; offset <= end; offset++)
                    {
                        bool match = true;
                        for (int index = 0; index < pattern.Length; index++)
                        {
                            if (!pattern[index].Wildcard && image[offset + index] != pattern[index].Value)
                            {
                                match = false;
                                break;
                            }
                        }
                        if (match)
                            matches.Add(offset);
                    }
                }
                return matches;
            }

            internal List<int> FindRelativeCallers(int targetRva)
            {
                var callers = new List<int>();
                foreach (Section section in executableSections)
                {
                    int end = Math.Min(image.Length, section.Start + section.Length) - 5;
                    for (int offset = section.Start; offset <= end; offset++)
                    {
                        if (image[offset] != 0xE8)
                            continue;
                        int target = unchecked(offset + 5 + BitConverter.ToInt32(image, offset + 1));
                        if (target == targetRva)
                            callers.Add(offset);
                    }
                }
                return callers;
            }

            internal byte[] ReadBytes(int rva, int count)
            {
                var result = new byte[count];
                Buffer.BlockCopy(image, rva, result, 0, count);
                return result;
            }

            private readonly struct Section
            {
                internal Section(int start, int length)
                {
                    Start = start;
                    Length = length;
                }

                internal int Start { get; }
                internal int Length { get; }
            }
        }
    }
}
