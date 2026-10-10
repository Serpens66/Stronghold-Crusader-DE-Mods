$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\Fremde Mods\crusader-de-tweaker-api-review'))
function UpdateFile([string]$relative, [scriptblock]$change) {
    $p = Join-Path $repo $relative
    $s = [IO.File]::ReadAllText($p).Replace("`r`n","`n")
    $s = (& $change $s).Replace("`r`n","`n").Replace("`n","`r`n")
    [IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
    if ([IO.File]::ReadAllText($p) -cne $s) { throw "Readback $relative" }
}
UpdateFile 'CrusaderDETweaker.csproj' {
    param($s)
    $s = $s.Replace('<Reference Include="System" />','<Reference Include="MessagePack"><HintPath>$(ShcdeseDir)\MessagePack.dll</HintPath><Private>False</Private></Reference>
    <Reference Include="MessagePack.Annotations"><HintPath>$(ShcdeseDir)\MessagePack.Annotations.dll</HintPath><Private>False</Private></Reference>
    <Reference Include="System" />')
    $s.Replace('<Compile Include="Config\PublicApi\ConfigurationApi.cs" />','<Compile Include="Config\PublicApi\ConfigurationApi.cs" />
    <Compile Include="Config\PublicApi\LobbyConfiguration.cs" />
    <Compile Include="Config\PublicApi\ConfigurationLobbyStorage.cs" />')
}
UpdateFile 'Config\Sync\ConfigSyncManager.cs' {
    param($s)
    $s = $s.Replace('private static bool _initialized;', 'private static bool _initialized;
        private static Configuration.ConfigurationLobbyStorage lobbyStorage;

        internal static void PreparePersistence(BaseUnityPlugin plugin)
        {
            lobbyStorage = new Configuration.ConfigurationLobbyStorage(plugin.Info.Location);
        }

        internal static void RequireLobbyPersistence()
        {
            if (lobbyStorage == null || !lobbyStorage.Loaded)
                throw new InvalidOperationException("Lobby configuration could not be loaded.", lobbyStorage?.LoadError);
        }')
    $s.Replace('PluginInfo.PLUGIN_NAME, Lobby, XamlPath);','PluginInfo.PLUGIN_NAME, Lobby, XamlPath, lobbyStorage);')
}
UpdateFile 'Plugin.cs' {
    param($s)
    $s = $s.Replace('SHCDESE.BepInEx.Bootstrap.Plugin.PLUGIN_GUID, "2.10.1"','SHCDESE.BepInEx.Bootstrap.Plugin.PLUGIN_GUID, "2.14.0"')
    $s = $s.Replace('            CrusaderDETweaker.Config.Sync.ConfigSyncManager.Register(this);','            // Recover all destinations before the Extender reads its lobby persistence.
            try
            {
                CrusaderDETweaker.Config.Sync.ConfigSyncManager.PreparePersistence(this);
                CrusaderDETweaker.Configuration.ConfigurationApi.ApplyPendingBeforeLoading();
                CrusaderDETweaker.Config.Sync.ConfigSyncManager.Register(this);
                CrusaderDETweaker.Config.Sync.ConfigSyncManager.RequireLobbyPersistence();
            }
            catch (Exception ex)
            {
                Logger.LogError("[ConfigurationApi] Startup recovery/loading failed; configuration loaders are blocked: " + ex);
                return;
            }')
    $s.Replace('                // Recover the complete file set before generators, bindings or loaders can read it.
                CrusaderDETweaker.Configuration.ConfigurationApi.ApplyPendingBeforeLoading();

','')
}
UpdateFile 'info.json' { param($s) $s.Replace('"MinimumScriptExtenderVersion": "2.10.1"','"MinimumScriptExtenderVersion": "2.14.0"') }
UpdateFile 'Config\PublicApi\ConfigurationFileTransaction.cs' {
    param($s)
    $s = $s.Replace('!after.ContainsKey(x) || after[x] == null','!after.ContainsKey(x) || (after[x] == null && (x != "LobbySettings.msgpack" || before[x] != null))')
    $s.Replace('if (after == null) throw new InvalidDataException("Missing target configuration: " + name);','if (after == null && (name != "LobbySettings.msgpack" || package.Before[name] != null))
                        throw new InvalidDataException("Missing target configuration: " + name);')
}
UpdateFile 'Tests\ConfigurationApi\ConfigurationApiTests.csproj' {
    param($s)
    $s = $s.Replace('<Reference Include="System" />','<Reference Include="MessagePack"><HintPath>$(GameDir)\BepInEx\plugins\000shcdese\MessagePack.dll</HintPath></Reference>
    <Reference Include="MessagePack.Annotations"><HintPath>$(GameDir)\BepInEx\plugins\000shcdese\MessagePack.Annotations.dll</HintPath></Reference>
    <Reference Include="System.Memory"><HintPath>$(GameDir)\BepInEx\plugins\000shcdese\System.Memory.dll</HintPath></Reference>
    <Reference Include="System" />')
    $s.Replace('<Compile Include="DocumentTests.cs" />','<Compile Include="DocumentTests.cs" />
    <Compile Include="..\..\Config\PublicApi\LobbyConfiguration.cs" />')
}
