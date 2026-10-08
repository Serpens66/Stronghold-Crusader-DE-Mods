$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\Fremde Mods\crusader-de-tweaker-api-review'))
function Save($name,$text) {
    $path=Join-Path $root $name
    $text=$text.Replace("`r`n","`n").Replace("`n","`r`n")
    [IO.File]::WriteAllText($path,$text,[Text.UTF8Encoding]::new($false))
    if ([IO.File]::ReadAllText($path) -cne $text) { throw "Readback mismatch: $name" }
}
$name='Tests/ConfigurationApi/DocumentTests.cs'
$text=[IO.File]::ReadAllText((Join-Path $root $name))
$text=$text.Replace('        values["units/Archer/MaxCount"] = 0L;', @'
        // The complete default snapshot must equal a fresh generated document set.
        // This is a document fixture test, not proof of native generator output.
        var expectedDefaults = new Dictionary<string, object>(values);
        var catalogDefaults = documents.Options.ToDictionary(option => option.Key, option => option.DefaultValue);
        Check(expectedDefaults.Count == catalogDefaults.Count && expectedDefaults.All(pair =>
            Equals(pair.Value, catalogDefaults[pair.Key])), "catalog defaults differ from freshly generated fixture files");
        config["Multipliers", "Health"].BoxedValue = 3f;
        config["Debug", "DebugLogging"].BoxedValue = true;
        var changedConfigCatalog = new ConfigurationDocuments(defaults, cfgName, config);
        Check(Equals(changedConfigCatalog.Options.Single(option => option.Key == "multipliers/Multipliers/Health").DefaultValue, 1d) &&
            Equals(changedConfigCatalog.Options.Single(option => option.Key == "multipliers/Debug/DebugLogging").DefaultValue, false),
            "CFG current values replaced registered defaults");
        values["units/Archer/MaxCount"] = 0L;
'@)
$text=$text.Replace('        values["units/Archer/Speed"] = double.NaN;', @'
        var reset = documents.Read(documents.Render(rendered, catalogDefaults));
        Check(expectedDefaults.Count == reset.Count && expectedDefaults.All(pair => Equals(pair.Value, reset[pair.Key])),
            "reset from changed documents did not restore every default");
        Check((long)reset["units/Archer/MaxCount"] == -1 && (long)roundtrip["units/Archer/MaxCount"] == 0,
            "reset confused unlimited with disabled");
        values["units/Archer/Speed"] = double.NaN;
'@)
Save $name $text
$name='Tests/ConfigurationApi/ContextApiTests.cs'
$text=[IO.File]::ReadAllText((Join-Path $root $name))
$text=$text.Replace('        bool callbackBlocked = false;', @'
        var startupDefaults = ConfigurationApi.GetOptions().ToDictionary(option => option.Key, option => option.DefaultValue);
        bool callbackBlocked = false;
'@)
$text=$text.Replace('        Check((long)ConfigurationApi.ReadOwnConfiguration().Values["units/Archer/Health"] == 10, "personal values polluted");', @'
        Check((long)ConfigurationApi.ReadOwnConfiguration().Values["units/Archer/Health"] == 10, "personal values polluted");
        CheckDefaults(startupDefaults, "temporary startup");
'@)
$text=$text.Replace('        VerifyNetworkRoundtrip(() => callbackBlocked);', '        VerifyNetworkRoundtrip(() => callbackBlocked);' + "`r`n" + '        CheckDefaults(startupDefaults, "host roundtrip and changed personal files");')
$text=$text.Replace('    private static void Start()', @'
    private static void CheckDefaults(Dictionary<string, object> expected, string phase)
    {
        var actual = ConfigurationApi.GetOptions().ToDictionary(option => option.Key, option => option.DefaultValue);
        Check(expected.Count == actual.Count && expected.All(pair => Equals(pair.Value, actual[pair.Key])),
            "Default capture was contaminated by " + phase);
    }

    private static void Start()
'@)
Save $name $text
