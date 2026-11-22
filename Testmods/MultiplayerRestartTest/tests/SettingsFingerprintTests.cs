using System;
using System.Collections.Generic;
using MessagePack;
using MultiplayerRestartTest;
using SHCDESE.API.Components.Network;

// Regression: incoming host values do not replace the client's own preset. Only
// the public synchronized host getters describe the configuration being restarted.
internal static class SettingsFingerprintTests
{
    private static int checks;
    private sealed class Endpoint
    {
        [SyncHostOnly] public int Amount { get; set; }
        [SyncHostOnly] public bool Enabled { get; set; }
        [SyncHostOnly] public string Rules { get; set; }
        [SyncHostOnly] public int[] Weights { get; set; }
        public int OwnPresetAmount { get; set; }
        public int LocalUiSize { get; set; }
        public Dictionary<string, byte[]> System_CreateCurrentWorkingSnapshot() =>
            new Dictionary<string, byte[]> { [nameof(Amount)] = MessagePackSerializer.Serialize(OwnPresetAmount) };
    }
    private sealed class BrokenEndpoint
    {
        [SyncHostOnly] public int Amount { get { throw new InvalidOperationException("unavailable host state"); } set { } }
    }
    private static void Check(bool value, string reason)
    { checks++; if (!value) throw new Exception(reason); }
    private static KeyValuePair<string, object> Entry(string id, object value) => new KeyValuePair<string, object>(id, value);
    private static string Hash(object value) => RestartSettingsFingerprint.Compute(new[] { Entry("mod", value) });
    internal static void Run()
    {
        var host = new Endpoint { Amount = 99, OwnPresetAmount = 99, Enabled = true, Rules = "original", Weights = new[] { 1, 2 } };
        var client = new Endpoint { Amount = 99, OwnPresetAmount = 17, Enabled = true, Rules = "original", Weights = new[] { 1, 2 }, LocalUiSize = 200 };
        Check(!Convert.ToBase64String(host.System_CreateCurrentWorkingSnapshot()["Amount"]).Equals(
            Convert.ToBase64String(client.System_CreateCurrentWorkingSnapshot()["Amount"])), "Fixture did not preserve different personal presets.");
        string agreed = Hash(host);
        Check(agreed == Hash(client), "Equal received host values were rejected because personal presets differ.");
        client.OwnPresetAmount = 42; client.LocalUiSize = 300;
        Check(agreed == Hash(client), "Personal-only changes altered the host fingerprint.");
        client.Amount = 98; Check(agreed != Hash(client), "A real host-value difference was ignored."); client.Amount = 99;
        client.Enabled = false; Check(agreed != Hash(client), "A real host toggle was ignored."); client.Enabled = true;
        client.Rules = "changed"; Check(agreed != Hash(client), "A host string difference was ignored."); client.Rules = "original";
        client.Weights[1] = 3; Check(agreed != Hash(client), "Host array contents were ignored."); client.Weights[1] = 2;
        Check(agreed == Hash(client), "Restoring effective values did not restore the fingerprint.");
        client.Rules = null; string absent = Hash(client); client.Rules = "";
        Check(absent != Hash(client), "Null and empty host strings became indistinguishable.");
        Check(RestartSettingsFingerprint.Compute(new[] { Entry("b", client), Entry("a", host) }) ==
            RestartSettingsFingerprint.Compute(new[] { Entry("a", host), Entry("b", client) }), "Registration order changed the fingerprint.");
        Check(RestartSettingsFingerprint.Compute(new[] { Entry("a", host) }) !=
            RestartSettingsFingerprint.Compute(new[] { Entry("b", host) }), "Endpoint identity was ignored.");
        bool failedClosed = false;
        try { Hash(new BrokenEndpoint()); } catch (System.Reflection.TargetInvocationException) { failedClosed = true; }
        Check(failedClosed, "Unreadable host values were silently omitted.");
        Console.WriteLine("Effective host settings fingerprints passed: " + checks + " checks.");
    }
}
