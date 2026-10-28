param([Parameter(Mandatory)][string]$AssemblyPath, [Parameter(Mandatory)][string]$ApiSharedDir, [Parameter(Mandatory)][string]$GameDir)
$ErrorActionPreference = 'Stop'
[void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $GameDir 'BepInEx/core/Mono.Cecil.dll')))
$resolver = [Mono.Cecil.DefaultAssemblyResolver]::new()
$resolver.AddSearchDirectory((Join-Path $GameDir 'BepInEx/core'))
$resolver.AddSearchDirectory((Join-Path $GameDir 'BepInEx/plugins/000shcdese'))
$resolver.AddSearchDirectory($ApiSharedDir)
$reader = [Mono.Cecil.ReaderParameters]::new(); $reader.AssemblyResolver = $resolver
$mod = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($AssemblyPath,$reader)
$api = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $ApiSharedDir 'APIShared.dll'))
$root = Split-Path -Parent $PSScriptRoot
try {
    $settings = $mod.MainModule.GetType('Spectator.SpectatorViewModel')
    foreach ($name in @('EnableMod','EnableSpectatorPerspective','EnableStatistics','EnableEliminatedPlayersBecomeSpectators')) {
        $property = $settings.Properties | Where-Object Name -eq $name
        if (-not ($property.CustomAttributes.AttributeType.Name -contains 'SyncHostOnlyAttribute')) { throw "Host role lost: $name" }
    }
    foreach ($name in @('EnableClientFeatures','EnableTestTools')) {
        $client = $settings.Properties | Where-Object Name -eq $name
        if (-not ($client.CustomAttributes.AttributeType.Name -contains 'PresetLocalAttribute')) { throw "Local tools must remain local settings: $name" }
    }
    $base = $api.MainModule.GetType('APIShared.ModSettings.PresetLobbyModSettingsViewModel')
    $items = @('ModeOptions','PropertyName','ScopeText','SelectedModeIndex','Title','Description','DisplayName','Name')
    foreach ($view in @(@('Patches/Assets/GUI/XAML/IngameUIScreens.xaml','Spectator.TestToolsViewModel'),@('Patches/Assets/GUI/XAMLResources/HUD_Main.xaml','Spectator.TestToolsViewModel'),@('Patches/Assets/GUI/XAMLResources/HUD_AlliesPanel.xaml','Spectator.AllyTestViewModel'),@('Override/ScriptExtenderUI/SpectatorSettings.xaml','Spectator.SpectatorViewModel'),@('Patches/Assets/GUI/XAMLResources/HUD_MissionOver.xaml','Spectator.StatisticsControlsViewModel'))) {
        $text = [IO.File]::ReadAllText((Join-Path $root $view[0]))
        if ($view[1] -eq 'Spectator.AllyTestViewModel') {
            [xml]$alliesXaml = $text
            # SetAttribute selectors name existing Vanilla bindings; only our added subtree uses this VM.
            $text = ($alliesXaml.SelectNodes('//Operation[@Type="Add"]/Content') | ForEach-Object { $_.OuterXml }) -join " "
        }
        $properties = @($mod.MainModule.GetType($view[1]).Properties.Name) + @($base.Properties.Name)
        foreach ($match in [regex]::Matches($text,'\{Binding ([\w]+)(?:[,}\[])')) {
            if ($match.Groups[1].Value -notin $properties -and $match.Groups[1].Value -notin $items) { throw "Unresolved binding: $($view[0]) $($match.Value)" }
        }
        $english = [IO.File]::ReadAllText((Join-Path $root 'Locales/en-US.txt'))
        foreach ($match in [regex]::Matches($text,'\{Binding Text\[([^\]]+)\]')) {
            if ($english -notmatch ('(?m)^Tools\.'+[regex]::Escape($match.Groups[1].Value)+'=')) { throw "Missing tool text: $($match.Value)" }
        }
    }
    [xml]$settingsXaml = [IO.File]::ReadAllText((Join-Path $root 'Override/ScriptExtenderUI/SpectatorSettings.xaml'))
    $hostContent = $settingsXaml.SelectSingleNode('//*[@IsEnabled="{Binding CanEditHostSettings}"]')
    if ($hostContent.Opacity -ne '{Binding HostSettingsOpacity}') { throw 'Host content must use the shared dimming contract.' }
    $runtime = $mod.MainModule.GetType('Spectator.SpectatorRuntime')
    $renderCalls = @($runtime.Methods | Where-Object HasBody | ForEach-Object { $_.Body.Instructions } | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.DeclaringType.FullName -eq 'APIShared.Presentation.VisibleMapRenderEvents' })
    if ($renderCalls.Count -eq 0) { throw 'Missing shared render registration.' }
    $dependencies = @($mod.MainModule.GetType('Spectator.SpectatorPlugin').CustomAttributes | Where-Object { $_.AttributeType.Name -eq 'BepInDependency' })
    if ($dependencies.Count -lt 2) { throw 'Missing runtime dependencies.' }
    if (@($dependencies | Where-Object { $_.ConstructorArguments[0].Value -match 'BugfixesAndQoL|SpectatorEditorBuildTest' }).Count) { throw 'Migration dependencies are not permitted.' }
    Write-Host 'PASS: compiled settings roles, shared preset bindings, tool bindings/locales and independent plugin dependencies.'
} finally { $mod.Dispose(); $api.Dispose() }
