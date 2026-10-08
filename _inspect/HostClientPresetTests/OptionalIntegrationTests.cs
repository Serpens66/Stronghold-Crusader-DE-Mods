using APIShared.ModSettings;
using System;
using System.IO;
using SerpsModsHost;
using Shared;

internal static class OptionalIntegrationTests
{
    internal static void Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "OptionalIntegrationTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string journal = Path.Combine(root, "RestartPreparation.json");
        ModSettingsApplication.ResetForTests(journal);
        var absent = new OptionalSettingsIntegration<Model>();
        int calls = 0, errors = 0;
        Func<Model> noWork = () => { calls++; return null; };
        absent.Discover(false, null, noWork, _ => errors++);
        Check(absent.State == OptionalSettingsState.Absent && calls == 0, "absent mod executed integration");
        absent.Discover(true, null, noWork, _ => errors++);
        Check(absent.State == OptionalSettingsState.Incompatible && calls == 0 && errors == 0, "official mod treated as failure");
        ProviderProbe.ConfigurationApi.Reset(3);
        ProviderProbe.ConfigurationApi.Ready = false;
        var waiting = new OptionalSettingsIntegration<Model>();
        Func<Model> activate = () =>
        {
            var provider = new StatsTweakerConfigurationProvider(typeof(ProviderProbe.ConfigurationApi));
            if (!provider.IsReady) return null;
            var model = new Model(provider);
            var candidate = LobbyModSettingsPresetRegistration.PrepareExternalWorkingCopy(null, Path.Combine(root,"probe.dll"), "Probe", "Probe", new Version(1,0), model);
            Check(!ModSettingsApplication.Endpoints.ContainsKey("Probe"), "preparation published participant");
            candidate.Activate(provider.EnableRestartManagedSynchronization, ex => ex.GetBaseException() is InvalidOperationException);
            return model;
        };
        waiting.Discover(true, typeof(ProviderProbe.ConfigurationApi), activate, _ => errors++);
        Check(waiting.State == OptionalSettingsState.Waiting && ProviderProbe.ConfigurationApi.Reads == 0, "not-ready provider read files");
        ProviderProbe.ConfigurationApi.Ready = true;
        waiting.Discover(true, typeof(ProviderProbe.ConfigurationApi), activate, _ => errors++);
        Check(waiting.State == OptionalSettingsState.Active && ModSettingsApplication.Endpoints.Count == 1, "valid provider not published");
        waiting.Discover(true, typeof(ProviderProbe.ConfigurationApi), activate, _ => errors++);
        waiting.Attach(_ => false, _ => errors++);
        Check(!waiting.ViewAttached && !waiting.ViewFailed, "unopened page became permanent failure");
        waiting.Attach(_ => { throw new IOException("Broken XAML"); }, _ => errors++);
        Check(waiting.State == OptionalSettingsState.Active && waiting.ViewFailed && ModSettingsApplication.Endpoints.Count == 1, "UI failure removed start guards");
        ModSettingsApplication.ResetForTests(journal);
        ProviderProbe.ConfigurationApi.Reset(3);
        ProviderProbe.ConfigurationApi.RejectEnable = true;
        var rejected = new OptionalSettingsIntegration<Model>();
        rejected.Discover(true, typeof(ProviderProbe.ConfigurationApi), activate, _ => errors++);
        Check(rejected.State == OptionalSettingsState.Failed && ModSettingsApplication.Endpoints.Count == 0 && !ModSettingsApplication.HasActivationFailures,
            "rejected opt-in leaked participant or start blocker");
        Check(ModSettingsApplication.PrepareLaunch(), "rejected optional candidate blocked unrelated launch");
        ProviderProbe.ConfigurationApi.Reset(3);
        var uncertainModel = new Model(new StatsTweakerConfigurationProvider(typeof(ProviderProbe.ConfigurationApi)));
        var uncertain = LobbyModSettingsPresetRegistration.PrepareExternalWorkingCopy(null, Path.Combine(root,"unknown.dll"), "Unknown", "Unknown", new Version(1,0), uncertainModel);
        try { uncertain.Activate(() => { throw new IOException("uncertain activation"); }); } catch (IOException) { }
        Check(ModSettingsApplication.HasActivationFailures, "uncertain activation lost its guard");
        bool blocked = false;
        try { ModSettingsApplication.PrepareLaunch(); } catch (InvalidOperationException) { blocked = true; }
        Check(blocked, "uncertain activation allowed start");
        ModSettingsApplication.ResetForTests(journal);
        File.WriteAllText(journal,"{}");
        Check(ModSettingsApplication.HasRestartPreparation && ModSettingsApplication.DescribeRestartPreparation().Length != 0, "corrupt preparation invisible without provider");
        string[] unresolved = ModSettingsApplication.DiscardPreparationWithReport();
        Check(unresolved.Length != 0 && !File.Exists(journal), "discard falsely claimed full cleanup");
        ProviderProbe.ConfigurationApi.Reset(0);
        bool invalidCatalog = false;
        try { new StatsTweakerConfigurationProvider(typeof(ProviderProbe.ConfigurationApi)); } catch (InvalidDataException) { invalidCatalog = true; }
        Check(invalidCatalog && ModSettingsApplication.Endpoints.Count == 0, "invalid catalog published");
        ProviderProbe.ConfigurationApi.Reset(3);
        ProviderProbe.ConfigurationApi.Version = 2;
        bool incompatible = false;
        try { new StatsTweakerConfigurationProvider(typeof(ProviderProbe.ConfigurationApi)); } catch (NotSupportedException) { incompatible = true; }
        Check(incompatible, "unknown API version accepted");
        bool missing = false;
        try { new StatsTweakerConfigurationProvider(typeof(MissingSignatureApi)); } catch (MissingMethodException) { missing = true; }
        Check(missing && ModSettingsApplication.Endpoints.Count == 0, "missing signature leaked participant");
        ProviderProbe.ConfigurationApi.Reset(3);
        var invalidProvider = new StatsTweakerConfigurationProvider(typeof(ProviderProbe.ConfigurationApi));
        invalidProvider.GetSettings()[0].DefaultValue = "wrong type";
        bool prepareFailed = false;
        try { LobbyModSettingsPresetRegistration.PrepareExternalWorkingCopy(null, Path.Combine(root,"invalid.dll"), "Invalid", "Invalid", new Version(1,0), new Model(invalidProvider)); }
        catch (ArgumentException) { prepareFailed = true; }
        Check(prepareFailed && ModSettingsApplication.Endpoints.Count == 0, "failed preparation published participant");
        ModSettingsApplication.ResetForTests(Path.Combine(root,"done.json"));
        Console.WriteLine("PASS: optional discovery, delayed readiness, real prepared registration, opt-in rejection, uncertain activation, UI isolation and provider-independent discard");
    }
    private static void Check(bool condition, string text) { if (!condition) throw new Exception(text); }
    public static class MissingSignatureApi { public static int ApiVersion => 1; }
    private sealed class Model : PresetLobbyModSettingsViewModel
    {
        private readonly StatsTweakerConfigurationProvider provider;
        internal Model(StatsTweakerConfigurationProvider provider) { this.provider = provider; }
        protected override IDynamicPresetSettingsProvider DynamicSettingsProvider => provider;
    }
}
