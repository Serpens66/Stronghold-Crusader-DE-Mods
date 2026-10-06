$ErrorActionPreference = 'Stop'
$path = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\Fremde Mods\crusader-de-tweaker-api-review\Tests\ConfigurationApi\StartupFailureTests.cs'))
$text = [IO.File]::ReadAllText($path)
$text = $text.Replace('"external"), legacy: true)', '"[Archer]\r\nHealth = 30\r\n"), legacy: true)')
$text = $text.Replace('        // ContextApiTests has initialized the facade. Startup rejection must retain cancellation.', @'
        // Finish initialization from the retained active files, as the plugin does after rejection.
        ConfigurationApi.CaptureGeneratedDefaults();
        var config = new BepInEx.Configuration.ConfigFile(Path.Combine(ConfigPaths.StartupConfigDir,
            "CrusaderDETweaker_GlobalMultipliers.cfg"), true);
        config.Bind("Debug", "Enabled", false, "Local");
        ConfigurationApi.CompleteInitialization(config);
        Check(ConfigurationApi.GetCapabilities().IsReady, scenario + " disabled the API");
        Check((long)ConfigurationApi.GetLoadedConfiguration().Values["units/Archer/Health"] == 10,
            scenario + " did not load the previous selected values");
'@)
$text = $text.Replace('File.WriteAllText(units, "external-during-recovery");', 'File.WriteAllText(units, "[Archer]\r\nHealth = 50\r\n");').Replace('File.WriteAllText(units, "before");','File.WriteAllText(units, "[Archer]\r\nHealth = 10\r\n");')
$text = $text.Replace('Check(!transaction.RecoveryRequired && transaction.ReadOwn().Values.All(bytes =>' + "`r`n" + '            System.Text.Encoding.UTF8.GetString(bytes) == "after"), "Legacy recovery failed to complete");', 'Check(!transaction.RecoveryRequired && File.ReadAllText(units).Contains("Health = 20"),' + "`r`n" + '            "Legacy recovery failed to complete");')
$text = $text.Replace('            File.WriteAllText(path, "before");', @'
            string filename = Path.GetFileName(name);
            string contents = filename == "units.toml" || filename == "structures.toml"
                ? (filename == "units.toml" ? "[Archer]" : "[Tower]") + "\r\nHealth = 10\r\n"
                : filename == "gameplay.toml" ? "[Flags]\r\nEnabled = false\r\n"
                : filename.EndsWith(".csv", StringComparison.Ordinal) ? ",Archer\r\nArcher,10\r\n"
                : "[Debug]\r\nEnabled = false\r\n";
            File.WriteAllText(path, contents);
'@)
$text = $text.Replace('        var after = before.ToDictionary(pair => pair.Key, pair => System.Text.Encoding.UTF8.GetBytes("after"));', @'
        // Legacy transactions do not include session files; seed the already-selected context separately.
        if (legacy)
        {
            foreach (string name in names)
            {
                string path = Path.Combine(session, name);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.Copy(Path.Combine(ConfigPaths.OwnConfigDir, name), path);
            }
        }
        var after = new Dictionary<string, byte[]>(before);
        after["units.toml"] = System.Text.Encoding.UTF8.GetBytes("[Archer]\r\nHealth = 20\r\n");
'@)
$text = $text.Replace("`r`n", "`n").Replace("`n", "`r`n")
[IO.File]::WriteAllText($path,$text,[Text.UTF8Encoding]::new($false))
if ([IO.File]::ReadAllText($path) -cne $text) { throw 'Readback mismatch' }
