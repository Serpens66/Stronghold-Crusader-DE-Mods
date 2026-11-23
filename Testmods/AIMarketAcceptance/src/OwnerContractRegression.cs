using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using APIShared.Economy;
using APIShared.Multiplayer;
using APIShared.Savegames;
using SHCDESE.API;
using SHCDESE.Interop;

namespace AIMarketAcceptance
{
    internal static partial class Acceptance
    {
        private static readonly byte[] ProbeRecord = { 0x50, 0x52, 0x42, 0x31 };
        private static byte[] probeCommitted;
        private static bool componentActive;
        private static int componentStarts, componentCompletes;
        private static int[] boundaryPending;

        private static void RegisterComponentContracts()
        {
            // This ordinary, foreign-owner record exercises registration alongside the market
            // consumer. The local component test never claims to exercise its MP transport.
            Require(MultiplayerResyncData.TryRegister(AcceptancePlugin.Guid, "component-probe", 1,
                ProbeRecord.Length, () => ProbeRecord, PrepareProbe, value => probeCommitted = value,
                out string reason), reason);
            Require(MultiplayerResyncEvents.TryRegister(AcceptancePlugin.Guid, "before-market-component",
                () => BeforeOwnerBoundary(true), () => BeforeOwnerBoundary(false), out reason, -101), reason);
            Require(MultiplayerResyncEvents.TryRegister(AcceptancePlugin.Guid, "after-market-component",
                () => AfterOwnerBoundary(true), () => AfterOwnerBoundary(false), out reason, -99), reason);
            Info("GENERIC_PROVIDER_REGISTERED id=component-probe transportTest=False");
        }

        private static byte[] PrepareProbe(ReadOnlyMemory<byte> bytes)
        {
            if (!bytes.Span.SequenceEqual(ProbeRecord)) throw new InvalidOperationException("Unexpected probe record.");
            return bytes.ToArray();
        }

        private static void RequireFixOn(string stage)
        {
            bool ready = (bool)owner.GetProperty("FeatureReady", Static).GetValue(null, null);
            int active = (int)owner.GetField("sessionActive", Static).GetValue(null);
            int corrective = (int)owner.GetField("corrective", Static).GetValue(null);
            int writer = ReadGate("writerGate"), receipt = ReadGate("receiptGate");
            bool correctionActive = (bool)owner.GetProperty("CorrectionActive", Static).GetValue(null, null);
            Require(ready && correctionActive && active == 1 && corrective == 1 && writer == 1 && receipt == 1,
                stage + ": fix not active; ready=" + ready + " session=" + active + " corrective=" + corrective +
                " writer=" + writer + " receipt=" + receipt);
            Info("FIX_ACTIVE stage=" + stage + " corrective=1 sessionActive=1 writerGate=1 receiptGate=1");
        }

        private static int ReadGate(string name)
        {
            IntPtr address = (IntPtr)owner.GetField(name, Static).GetValue(null);
            Require(address != IntPtr.Zero, "Missing native gate " + name);
            return Marshal.ReadInt32(address);
        }

        private static void VerifyPureCapture()
        {
            var requests = (Array)owner.GetField("requests", Static).GetValue(null);
            int index = player * (int)eGoods.Count + good;
            object original = requests.GetValue(index);
            int[] pending = CaptureNativePending();
            Require(ContainsSelectedRow(CaptureOwnerBytes()), "Valid capture omitted the real owned request.");
            try
            {
                // Negative fixture changes a private proof only, never a native purchase cell.
                // Preserve a genuinely produced row so all later source/save tests stay genuine.
                object invalid = requests.GetValue(index), target = Field(invalid, "Target");
                long token = (long)Field(target, "PolicyToken");
                target.GetType().GetField("PolicyToken", Instance).SetValue(target, token ^ 1L);
                invalid.GetType().GetField("Target", Instance).SetValue(invalid, target);
                requests.SetValue(invalid, index);
                byte[] encoded = CaptureOwnerBytes();
                Require(!ContainsSelectedRow(encoded), "Capture retained a row with an invalid policy proof.");
                Require(invalid.Equals(requests.GetValue(index)), "Capture changed the live private request.");
                RequirePendingEqual(pending, "pure-capture");
            }
            finally { requests.SetValue(original, index); }
            RequireState(Read(player, good), "after-pure-capture");
            Info("CAPTURE_PURITY_PASS actualOwner=True invalidRowOmitted=True liveLedgerUnchanged=True nativePendingUnchanged=True");
        }

        private static byte[] CaptureOwnerBytes()
        {
            Assembly apiAssembly = typeof(VersionedSaveWriter).Assembly;
            Type knownType = apiAssembly.GetType("APIShared.Savegames.VersionedSaveKnownEntries", true);
            object[] arguments = { new[] { "ledger" }, null, null };
            Require((bool)knownType.GetMethod("TryCreate", Static).Invoke(null, arguments),
                "Could not prepare an owned capture writer: " + arguments[2]);
            var writer = (VersionedSaveWriter)Activator.CreateInstance(typeof(VersionedSaveWriter), Instance,
                null, new[] { arguments[1], (object)true, false, "component-capture" }, null);
            try
            {
                Require((bool)Call(owner, "CaptureLedger", writer), "Owner capture declined its snapshot.");
                var entries = (IDictionary)typeof(VersionedSaveWriter).GetProperty("Written", Instance).GetValue(writer, null);
                return (byte[])entries["ledger"];
            }
            finally { typeof(VersionedSaveWriter).GetMethod("Close", Instance).Invoke(writer, null); }
        }

        private static bool ContainsSelectedRow(byte[] encoded)
        {
            Type codec = FindType("AIMarketOverbuyDiagnostic.MarketDemandSaveCodec");
            foreach (object row in (Array)Call(codec, "Decode", encoded))
                if ((int)Field(row, "Player") == player && (int)Field(row, "Good") == good) return true;
            return false;
        }

        private static void VerifyOwnerRepairCallbacks()
        {
            object registry = typeof(MultiplayerResyncEvents).GetField("Registry", Static).GetValue(null);
            var entries = (Array)registry.GetType().GetField("registrations", Instance).GetValue(registry);
            // Dispatch real registered callbacks only in the isolated acceptance layout. A
            // foreign testmod's callback must never be activated by this local component probe.
            foreach (object entry in entries)
            {
                string registered = (string)Field(entry, "Owner");
                Require(registered == AcceptancePlugin.Guid || registered == "AIMarketOverbuyDiagnostic_Serp",
                    "Owner component test requires an isolated layout; another resync participant is registered: " + registered);
            }
            Require(!MultiplayerResyncEvents.IsInProgress, "A real repair is already active.");
            var requests = (Array)owner.GetField("requests", Static).GetValue(null);
            var depths = (int[])owner.GetField("decisionDepth", Static).GetValue(null);
            Array beforeRequests = (Array)requests.Clone();
            int[] beforeDepths = (int[])depths.Clone();
            object beforeWorld = owner.GetField("worldState", Static).GetValue(null);
            object priorCurrent = registry.GetType().GetField("current", Instance).GetValue(registry);
            object priorFault = registry.GetType().GetField("faulted", Instance).GetValue(registry);
            int[] pending = CaptureNativePending();
            long originalSequence = Read(player, good).Sequence;
            bool freshSourceEstablished = false;
            componentActive = true;
            try
            {
                InvokeRegistry(registry, "Start", 1); InvokeRegistry(registry, "Start", 2);
                RequireFixOn("ignored-pack-size");
                Require(componentStarts == 0 && componentCompletes == 0, "PACK/SIZE dispatched a boundary.");
                InvokeRegistry(registry, "Start", 0);
                Require(componentStarts == 1 && MultiplayerResyncEvents.IsInProgress, "Executed start did not suspend the actual owner.");
                try { CaptureOwnerBytes(); throw new InvalidOperationException("Repair-time capture was accepted."); }
                catch (TargetInvocationException error)
                {
                    Require(error.InnerException is InvalidOperationException && error.InnerException.Message.Contains("during repair"),
                        "Unexpected repair-time capture failure: " + error.InnerException);
                }
                // Completion must discard repair scratch too. This is private test data, not
                // a fabricated native demand; failure cleanup restores the original fixture.
                requests.SetValue(beforeRequests.GetValue(player * (int)eGoods.Count + good), player * (int)eGoods.Count + good);
                InvokeRegistry(registry, "Complete", 1); InvokeRegistry(registry, "Complete", 2);
                Require(componentCompletes == 0 && MultiplayerResyncEvents.IsInProgress, "PACK/SIZE resumed the owner.");
                InvokeRegistry(registry, "Complete", 0);
                Require(componentCompletes == 1 && !MultiplayerResyncEvents.IsInProgress, "Completion did not rearm the actual owner.");
                RequirePendingEqual(pending, "owner-repair-component");
                Require(!(bool)Field(Field(requests.GetValue(player * (int)eGoods.Count + good), "Target"), "Active"),
                    "An unknown native pending value became owned after reset.");
                Info("OWNER_REPAIR_COMPONENT_PASS actualOwner=True orderedApiRegistry=True nativePendingPreserved=True " +
                    "startCleared=True completionCleared=True gatesRearmed=True nativeChoresExecuted=False dataTransportExecuted=False genericCommitObserved=" +
                    (probeCommitted != null));
                EstablishFreshNativePackage(originalSequence);
                freshSourceEstablished = true;
            }
            finally
            {
                componentActive = false;
                // Isolated component fixture cleanup only: no native repair was started.
                registry.GetType().GetField("inProgress", Instance).SetValue(registry, 0);
                registry.GetType().GetField("current", Instance).SetValue(registry, priorCurrent);
                registry.GetType().GetField("faulted", Instance).SetValue(registry, priorFault);
                // On success retain only genuinely reopened native requests for the later
                // save test. A failed probe restores its private fixture before quitting.
                if (!freshSourceEstablished) Array.Copy(beforeRequests, requests, requests.Length);
                Array.Copy(beforeDepths, depths, depths.Length);
                owner.GetField("worldState", Static).SetValue(null, beforeWorld);
                Call(owner, "PublishWorldGate");
            }
            RequireFixOn("after-owner-component-cleanup");
        }

        private static unsafe void EstablishFreshNativePackage(long previousSequence)
        {
            RequireFixOn("before-new-native-source");
            var players = GamePlayerManagerAPI.Instance;
            Require(players.TryGetPlayerResourcesById(player, out GamePlayerResources* resources) && resources != null,
                "Reopened source player is unavailable.");
            int attempts = RecruitmentAttemptBound(resources), reopened = 0;
            for (int attempt = 0; attempt < attempts && reopened == 0; attempt++)
            {
                recruit(new IntPtr(module + 0x404C950), player);
                for (int g = (int)eGoods.STORED_BOWS; g <= (int)eGoods.STORED_METAL_ARMOUR; g++)
                {
                    State next = Read(player, g);
                    if (next.Active && next.Pending == 5 && next.Expected == 5 && next.Package == 5 &&
                        next.Credit == 0 && next.Stock == 0 && next.Sequence > previousSequence)
                    { reopened = g; break; }
                }
            }
            Require(reopened != 0, "No fresh native package was observed after the owner reset; no manufactured request.");
            good = reopened;
            Require(players.TryAddGood(player, (eGoods)good, 1), "Fresh-source partial delivery failed.");
            players.RemoveGood(player, (eGoods)good, 1);
            RequireState(Read(player, good), "fresh-native-source-after-reset");
            Info("OWNER_NEW_SOURCE_PASS player=" + player + " good=" + good +
                " newPackage=5 initialCredit=0 physicalDelivery=1 immediateConsumption=1 pending=4 credited=1 fixturePendingWrites=0");
        }

        private static void InvokeRegistry(object registry, string name, int entryPhase) =>
            registry.GetType().GetMethod(name, Instance).Invoke(registry, new object[] { entryPhase });

        private static void BeforeOwnerBoundary(bool starting)
        {
            if (!componentActive) return;
            boundaryPending = CaptureNativePending();
            Require(starting || ReadGate("writerGate") == 0 && ReadGate("receiptGate") == 0,
                "The owner resumed before completion dispatch.");
        }

        private static void AfterOwnerBoundary(bool starting)
        {
            if (!componentActive) return;
            RequirePendingEqual(boundaryPending, starting ? "start-boundary" : "complete-boundary");
            var requests = (Array)owner.GetField("requests", Static).GetValue(null);
            foreach (object request in requests)
                Require(!(bool)Field(Field(request, "Target"), "Active"), "Actual owner retained a request across a repair boundary.");
            object world = owner.GetField("worldState", Static).GetValue(null);
            Require((bool)Field(world, "InRepair") == starting, "Owner repair state differs from the dispatched boundary.");
            if (starting)
            {
                Require((int)owner.GetField("sessionActive", Static).GetValue(null) == 0 &&
                    ReadGate("writerGate") == 0 && ReadGate("receiptGate") == 0, "Repair start left native producer gates active.");
                componentStarts++;
            }
            else { RequireFixOn("completion-after-owner"); componentCompletes++; }
        }

        private static unsafe int[] CaptureNativePending()
        {
            var values = new List<int>();
            for (int p = 1; p <= GamePlayerManagerAPI.MAX_PLAYERS; p++)
            {
                bool found = GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(p, out GamePlayerResources* resources) && resources != null;
                values.Add(found ? 1 : 0);
                if (!found) continue;
                // Complete audited native array, including its Null and Gold sentinel cells.
                int* pending = (int*)&resources->r_AIPendingMarketPurchaseAmountNull;
                for (int g = 0; g < (int)eGoods.Count; g++) values.Add(pending[g]);
            }
            return values.ToArray();
        }

        private static void RequirePendingEqual(int[] expected, string stage)
        {
            int[] actual = CaptureNativePending();
            Require(expected != null && expected.Length == actual.Length, stage + ": native pending layout changed.");
            for (int i = 0; i < actual.Length; i++) Require(expected[i] == actual[i], stage + ": native pending changed at cell " + i);
        }
    }
}
