using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using APIShared.Savegames;
using SHCDESE.API;
using SHCDESE.Interop;

namespace AIMarketAcceptance
{
    internal static partial class Acceptance
    {
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
            Type codec = FindType("BugfixesAndQoL.AI.Market.MarketDemandSaveCodec");
            foreach (object row in (Array)Call(codec, "Decode", encoded))
                if ((int)Field(row, "Player") == player && (int)Field(row, "Good") == good) return true;
            return false;
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
