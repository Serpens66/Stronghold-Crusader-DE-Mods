using APIShared.ModData;
using ExtendedData;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

internal static class ModDataSourceTests
{
    internal static void Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "ExtendedData-source-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string lord = Path.Combine(root, "config.lordjson");
        try
        {
            ExtendedDataModDataApi.UnregisterSource();
            ExtendedDataModDataApi.InitializeSource();
            File.WriteAllText(Path.Combine(root, "config.modlord.json"), "{\"third.party\":{\"value\":7}}", new UTF8Encoding(false));
            ExtendedDataModDataApi.SetSinglePlayerLords(new Dictionary<int, string> { [2] = lord }, Array.Empty<int>());
            Check(Value(ModDataAccess.ReadSelectedLordNamespace(2, "third.party")) == 7, "Shared API missed the verified single-player source.");
            Check(ExtendedDataModDataApi.ReadSelectedLordNamespace(2, "third.party").Success, "Legacy facade missed the shared source.");

            ExtendedDataModDataApi.SetNetworkSnapshot(null, true);
            Check(ModDataAccess.ReadSelectedLordNamespace(2, "third.party").Status == ModDataReadStatus.HostDataUnavailable,
                "A prior local selection leaked into unconfirmed host data.");
            Check(ExtendedDataModDataApi.ReadLordNamespace(lord, "third.party").Status == ExtendedDataModDataReadStatus.HostDataUnavailable,
                "The legacy path overload substituted a local file for missing host data.");

            var snapshot = LordDataSnapshot.Create("confirmed", false, new[] { new LordDataSlot {
                PlayerId = 2, LordName = "Lord", ConfigName = "config", ConfigChecksum = "123",
                ModLordJson = "{\"third.party\":{\"value\":99}}" } });
            ExtendedDataModDataApi.SetNetworkSnapshot(snapshot, true);
            var retained = ModDataAccess.ReadSelectedLordNamespace(2, "third.party");
            Check(Value(retained) == 99 && retained.Source.StartsWith("host Lord slot 2", StringComparison.Ordinal), "Confirmed host source was not authoritative.");
            Check(Value(ModDataAccess.ReadLocalLordNamespace(lord, "third.party")) == 7, "Explicit local reads did not retain local provenance.");
            Check(Convert.ToInt32(ExtendedDataModDataApi.ReadLordNamespace(lord, "third.party").Data["value"]) == 99,
                "Legacy selected-path behavior changed to local data.");
            ExtendedDataModDataApi.SetNetworkSnapshot(null, false);
            Check(ModDataAccess.ReadSelectedLordNamespace(2, "third.party").Status == ModDataReadStatus.HostDataUnavailable,
                "End/reset retained confirmed host data.");
            Check(Value(retained) == 99, "A previously returned immutable snapshot changed after reset.");

            ExtendedDataModDataApi.UnregisterSource();
            Check(!ModDataAccess.HasSelectedLordSource, "Initialization-failure cleanup left a provider published.");
            ExtendedDataModDataApi.SetNetworkSnapshot(snapshot, true);
            Check(!ModDataAccess.HasSelectedLordSource, "A delayed retired callback republished the provider.");
            ExtendedDataModDataApi.InitializeSource();
            Check(ModDataAccess.HasSelectedLordSource && ModDataAccess.ReadSelectedLordNamespace(2, "third.party").Status == ModDataReadStatus.HostDataUnavailable,
                "Re-registration retained a previous selection.");
        }
        finally { ExtendedDataModDataApi.UnregisterSource(); Directory.Delete(root, true); }
    }
    private static int Value(ModDataReadResult result) => result.Success ? Convert.ToInt32(result.Data["value"]) : throw new InvalidOperationException(result.Diagnostic);
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
