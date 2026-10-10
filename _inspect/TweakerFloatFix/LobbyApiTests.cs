using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using CrusaderDETweaker.Configuration;
using CrusaderDETweaker.Config.Toml;

internal static class LobbyApiTests
{
    private const string Cap = "LobbyMaxCounts/Archer";
    private static string path, active;
    private static LobbyConfiguration document;

    internal static void Run(string root)
    {
        foreach (string culture in new[] { "de-DE", "en-US" })
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            ConfigPaths.OwnConfigDir = Path.Combine(root, "lobby-api-" + culture);
            ConfigPaths.StartupContextId = "";
            Directory.CreateDirectory(ConfigPaths.OwnConfigDir);
            path = Path.Combine(root, "plugin-" + culture, "LobbyModSettings", "Tweaker.msgpack");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            document = new LobbyConfiguration(new[] { "Archer", "Tower" }, Parse,
                values => string.Join(";", values.OrderBy(x => x.Key).Select(x => x.Key + "=" + x.Value.ToString(CultureInfo.InvariantCulture))),
                text => text == "" || (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)
                    && n >= -1 && n <= 100000 && text == n.ToString(CultureInfo.InvariantCulture)));
            ConfigurationApi.ConfigureLobby(document, path, () => active, value => active = value);
            ConfigurationApi.NetworkClientCheck = () => false;
            ConfigGenerator.GenerateDefaultConfigGlobals();
            ConfigGenerator.GenerateDefaultConfigUnits();
            ConfigGenerator.GenerateDefaultConfigStructures();
            CrusaderDETweaker.Config.DamageMatrix.DamageMatrixManager.GenerateDefaults();
            byte[] original = LobbyConfiguration.WithValue(null, "OtherModSetting", 12345);
            original = LobbyConfiguration.WithValue(original, "MaxCounts", "Archer=5");
            File.WriteAllBytes(path, original);
            Start();

            var own = ConfigurationApi.ReadOwnConfiguration();
            Check((string)own.Values[Cap] == "5", "personal cap missing");
            Check(!ConfigurationApi.GetOptions().Single(x => x.Key == Cap).RequiresRestart, "lobby option requires restart");
            Check(ConfigurationApi.GetCapabilities().CanApplyLiveSubset && !ConfigurationApi.GetCapabilities().CanApplyWithoutRestart,
                "partial application misrepresented as full runtime support");
            var target = new Dictionary<string, object>(own.Values);
            foreach (string value in new[] { "", "-1", "0", "17" })
            {
                own = ConfigurationApi.ReadOwnConfiguration();
                target[Cap] = value;
                ConfigurationApi.ApplyLiveConfiguration(target, "", own.Revision);
                Check((string)ConfigurationApi.GetActiveConfiguration().Values[Cap] == value, "live cap not applied");
                Check((string)ConfigurationApi.GetLoadedConfiguration().Values[Cap] == "5", "startup snapshot mutated");
                Check(ConfigurationApi.GetPendingConfiguration() == null, "live application created a restart request");
                Check(LobbyConfiguration.Decode(File.ReadAllBytes(path))["OtherModSetting"].SequenceEqual(
                    LobbyConfiguration.Decode(original)["OtherModSetting"]), "unrelated MessagePack field lost");
            }

            own = ConfigurationApi.ReadOwnConfiguration();
            byte[] personal = File.ReadAllBytes(path);
            target[Cap] = "3";
            ConfigurationApi.ApplyLiveConfiguration(target, "trail-live", own.Revision);
            Check(personal.SequenceEqual(File.ReadAllBytes(path)), "temporary live cap overwrote personal value");
            Check((string)ConfigurationApi.ReadOwnConfiguration().Values[Cap] == "17", "temporary cap returned as personal");
            Check(ConfigurationApi.GetActiveConfiguration().ContextId == "trail-live", "live context not reported");
            own = ConfigurationApi.ReadOwnConfiguration();
            ConfigurationApi.ApplyLiveConfiguration(own.Values, "", own.Revision);
            Check((string)ConfigurationApi.GetActiveConfiguration().Values[Cap] == "17", "personal live return failed");

            own = ConfigurationApi.ReadOwnConfiguration();
            target = new Dictionary<string, object>(own.Values);
            target["units/Archer/Health"] = 20L;
            target[Cap] = "0";
            Throws(() => ConfigurationApi.ApplyLiveConfiguration(target, "", own.Revision));
            Check((string)ConfigurationApi.GetActiveConfiguration().Values[Cap] == "17", "rejected mixed application changed lobby");
            ConfigurationApi.StageContextConfiguration(target, "trail-restart", own.Revision);
            byte[] pending = File.ReadAllBytes(Path.Combine(ConfigPaths.OwnConfigDir, "PendingConfiguration.bin"));
            target[Cap] = "100001";
            Throws(() => ConfigurationApi.StageConfiguration(target, own.Revision));
            Check(pending.SequenceEqual(File.ReadAllBytes(Path.Combine(ConfigPaths.OwnConfigDir, "PendingConfiguration.bin"))), "invalid replacement lost pending");
            Start();
            Check((long)ConfigurationApi.GetActiveConfiguration().Values["units/Archer/Health"] == 20L &&
                (string)ConfigurationApi.GetActiveConfiguration().Values[Cap] == "0", "mixed startup not complete");
            Check(personal.SequenceEqual(File.ReadAllBytes(path)), "temporary restart changed personal lobby bytes");
            ConfigurationApi.StageReturnToOwnConfiguration();
            Start();
            Check((string)ConfigurationApi.GetActiveConfiguration().Values[Cap] == "17", "personal restart return failed");

            own = ConfigurationApi.ReadOwnConfiguration();
            target = new Dictionary<string, object>(own.Values) { [Cap] = "8" };
            ConfigurationApi.StageConfiguration(target, own.Revision);
            pending = File.ReadAllBytes(Path.Combine(ConfigPaths.OwnConfigDir, "PendingConfiguration.bin"));
            File.WriteAllBytes(path, LobbyConfiguration.WithValue(File.ReadAllBytes(path), "MaxCounts", "Archer=9"));
            Throws(() => ConfigurationApi.ApplyLiveConfiguration(target, "", own.Revision));
            Start();
            Check((string)ConfigurationApi.GetActiveConfiguration().Values[Cap] == "9", "external edit overwritten at startup");
            Check(pending.SequenceEqual(File.ReadAllBytes(Path.Combine(ConfigPaths.OwnConfigDir, "PendingConfiguration.bin"))), "conflict discarded pending");
            ConfigurationApi.DiscardPendingConfiguration();

            Console.WriteLine("PASS: " + culture + " lobby snapshot, scalar sentinels, live/mixed application, personal isolation and conflicts");
        }
        VerifyRecovery(root);
    }

    private static void VerifyRecovery(string root)
    {
        foreach (string checkpoint in new[] { "journal-published", "written:LobbySettings.msgpack", "written:units.toml", "verified", "completed" })
        {
            string folder = Path.Combine(root, "lobby-recovery-" + checkpoint.Replace(':', '-'));
            Directory.CreateDirectory(folder);
            string external = Path.Combine(folder, "plugin", "settings.msgpack");
            Directory.CreateDirectory(Path.GetDirectoryName(external));
            File.WriteAllBytes(external, new byte[] { 1 });
            File.WriteAllText(Path.Combine(folder, "units.toml"), "before");
            var tx = new ConfigurationFileTransaction(folder, new[] { "units.toml", LobbyConfiguration.FileName }, "test", external);
            var before = tx.ReadOwn();
            var after = new Dictionary<string, byte[]>(before)
            {
                [LobbyConfiguration.FileName] = new byte[] { 2 }, ["units.toml"] = new byte[] { 3 }
            };
            tx.Stage(after, ConfigurationFileTransaction.Revision(before));
            tx.Checkpoint = mark => { if (mark == checkpoint) throw new IOException("interrupted"); };
            Throws(() => tx.ApplyBeforeLoading());
            tx.Checkpoint = null;
            tx.ApplyBeforeLoading();
            Check(ConfigurationFileTransaction.Revision(after) == ConfigurationFileTransaction.Revision(tx.ReadOwn()), "external target recovery failed");
        }
        Console.WriteLine("PASS: lobby file recovery at every changed-file boundary; package paths remain logical");
    }

    private static Dictionary<string, int> Parse(string text) => string.IsNullOrEmpty(text)
        ? new Dictionary<string, int>()
        : text.Split(';').Select(x => x.Split('=')).ToDictionary(x => x[0], x => int.Parse(x[1], CultureInfo.InvariantCulture));

    private static void Start()
    {
        ConfigurationApi.ApplyPendingBeforeLoading();
        active = LobbyConfiguration.ReadEncoded(File.Exists(ConfigurationApi.SelectedLobbyPath)
            ? File.ReadAllBytes(ConfigurationApi.SelectedLobbyPath) : null);
        ConfigurationApi.CaptureGeneratedDefaults();
        var cfg = new ConfigFile(Path.Combine(ConfigPaths.StartupConfigDir, "CrusaderDETweaker_GlobalMultipliers.cfg"), true);
        cfg.Bind("Multipliers", "Health", 1f, "fixture");
        cfg.Bind("Debug", "DebugLogging", false, "fixture");
        cfg.Save();
        ConfigurationApi.CompleteInitialization(cfg);
    }

    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Throws(Action action) { try { action(); } catch { return; } throw new Exception("Expected rejection"); }
}
