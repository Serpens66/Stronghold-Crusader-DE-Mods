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
$game = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GameDir 'Stronghold Crusader Definitive Edition_Data/Managed/Assembly-CSharp.dll'))
$root = Split-Path -Parent $PSScriptRoot
try {
    $settings = $mod.MainModule.GetType('Spectator.SpectatorViewModel')
    foreach ($name in @('EnableMod','AllowEnemyDetails','EnableEliminatedPlayersBecomeSpectators')) {
        $property = $settings.Properties | Where-Object Name -eq $name
        if (-not ($property.CustomAttributes.AttributeType.Name -contains 'SyncHostOnlyAttribute')) { throw "Host role lost: $name" }
        if (-not ($property.CustomAttributes.AttributeType.Name -contains 'MissionIndependentAttribute')) { throw "Mission-independent ownership lost: $name" }
    }
    foreach ($name in @('EnableClientFeatures','EnableTestTools','EnableSingleplayerTestMode','EnableSpectatorPerspective','EnableSpectatorControl','EnableStatistics','ShowDateWhenHudHidden','EnableLordOverview','OverviewColumns','OverviewDesign','OverviewLordGrid','OverviewValueGrid','OverviewShowEliminated','OverviewSortBy','OverviewSortDescending')) {
        $client = $settings.Properties | Where-Object Name -eq $name
        if (-not ($client.CustomAttributes.AttributeType.Name -contains 'PresetLocalAttribute')) { throw "Local tools must remain local settings: $name" }
    }
    $base = $api.MainModule.GetType('APIShared.ModSettings.PresetLobbyModSettingsViewModel')
    $items = @('ModeOptions','PropertyName','ScopeText','SelectedModeIndex','Title','Description','DisplayName','Name')
    foreach ($view in @(@('Patches/Assets/GUI/XAML/IngameUIScreens.xaml','Spectator.TestToolsViewModel'),@('Override/ScriptExtenderUI/SpectatorSettings.xaml','Spectator.SpectatorViewModel'),@('Patches/Assets/GUI/XAMLResources/HUD_MissionOver.xaml','Spectator.StatisticsControlsViewModel'))) {
        $text = [IO.File]::ReadAllText((Join-Path $root $view[0]))
        if ($view[0] -eq 'Patches/Assets/GUI/XAML/IngameUIScreens.xaml') {
            [xml]$document = $text
            $ns = [Xml.XmlNamespaceManager]::new($document.NameTable); $ns.AddNamespace('x','http://schemas.microsoft.com/winfx/2006/xaml')
            $parentStyle = $document.SelectSingleNode('//*[@x:Name="SpectatorEditorHumanCanvas"]/Canvas.Style',$ns)
            if (-not $parentStyle) { throw 'Missing native-context visibility gate.' }
            $nativeProperties = @($game.MainModule.GetType('CrusaderDE.MainViewModel').Properties | Where-Object { $_.GetMethod.IsPublic } | ForEach-Object Name)
            foreach ($binding in [regex]::Matches($parentStyle.OuterXml,'\{Binding ([\w]+)(?:[,}\[])')) {
                if ($binding.Groups[1].Value -notin $nativeProperties) { throw "Unresolved native-context binding: $($binding.Value)" }
            }
            [void]$parentStyle.ParentNode.RemoveChild($parentStyle)
            $text = $document.OuterXml
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
    # Structural search/category coverage; preset and role behavior are tested separately.
    $sectionKeys = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($group in @(
        @{Controls=@('EnableEliminatedPlayersBecomeSpectators');Role='CanEditMissionIndependentHostSettings'},
        @{Controls=@('AllowEnemyDetails');Role='CanEditMissionIndependentHostSettings'},
        @{Controls=@('EnableLordOverview','OverviewDesignIndex');Role='CanEditClientSettings'},
        @{Controls=@('EnableSpectatorPerspective','EnableSpectatorControl','EnableStatistics','ShowDateWhenHudHidden');Role='CanEditClientSettings'},
        @{Controls=@('EnableTestTools','EnableSingleplayerTestMode');Role='CanEditClientSettings'})) {
        $section = $null
        foreach ($property in $group.Controls) {
            $control = $settingsXaml.SelectSingleNode('//*[@IsChecked="{Binding '+$property+', Mode=TwoWay}" or @SelectedIndex="{Binding '+$property+', Mode=TwoWay}"]')
            $owner = $control
            while ($owner -is [Xml.XmlElement] -and $owner.GetAttribute('shared:ModSettingsSearch.IsSection') -ne 'True') { $owner = $owner.ParentNode }
            if (-not $control -or $owner -isnot [Xml.XmlElement]) { throw "Setting lacks a searchable topic: $property" }
            if ($section -and $section -ne $owner) { throw 'Related settings must share their topic.' }
            $section = $owner
        }
        $title = $section.GetAttribute('shared:ModSettingsSearch.SectionTitle')
        $heading = $section.SelectSingleNode('*[local-name()="TextBlock"]')
        if (-not $sectionKeys.Add($section.GetAttribute('shared:ModSettingsSearch.SectionKey')) -or -not $title -or $heading.Text -ne $title) { throw 'Topics require distinct search keys and matching localized headings.' }
        if ($section.ParentNode.GetAttribute('IsEnabled') -ne ('{Binding '+$group.Role+'}')) { throw 'Topics must retain the appropriate host/local access gate.' }
    }
    $hostContent = $settingsXaml.SelectSingleNode('//*[@IsEnabled="{Binding CanEditMissionIndependentHostSettings}"]')
    if (-not $hostContent) { throw 'Host options must use the mission-independent authority binding.' }
    foreach ($hostControl in @($hostContent) + @($hostContent.SelectNodes('.//*'))) {
        for ($ancestor = $hostControl; $ancestor; $ancestor = $ancestor.ParentNode) {
            if ($ancestor -is [Xml.XmlElement] -and $ancestor.GetAttribute('IsEnabled') -match 'CanEditHostSettings|AreSettingsEditable') {
                throw 'An ancestor still disables mission-independent host options through a mission lock.'
            }
        }
    }
    $hostToggle = $settingsXaml.SelectSingleNode('//*[@IsChecked="{Binding HostSettingsEnabled, Mode=TwoWay}"]')
    if ($hostToggle.GetAttribute('IsEnabled') -ne '{Binding CanToggleHostSettings}') { throw 'Host activation must use the classification-aware shared gate.' }
    for ($ancestor = $hostToggle.ParentNode; $ancestor; $ancestor = $ancestor.ParentNode) {
        if ($ancestor -is [Xml.XmlElement] -and $ancestor.GetAttribute('IsEnabled') -match 'CanEditHostSettings|AreSettingsEditable') { throw 'Host activation is inside a mission-disabled container.' }
    }
    if ($hostContent.Opacity -ne '{Binding HostSettingsOpacity}') { throw 'Host content must use the shared dimming contract.' }
    $runtime = $mod.MainModule.GetType('Spectator.SpectatorRuntime')
    $renderCalls = @($runtime.Methods | Where-Object HasBody | ForEach-Object { $_.Body.Instructions } | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.DeclaringType.FullName -eq 'APIShared.Presentation.VisibleMapRenderEvents' })
    if ($renderCalls.Count -eq 0) { throw 'Missing shared render registration.' }
    $dependencies = @($mod.MainModule.GetType('Spectator.SpectatorPlugin').CustomAttributes | Where-Object { $_.AttributeType.Name -eq 'BepInDependency' })
    if ($dependencies.Count -lt 2) { throw 'Missing runtime dependencies.' }
    if (@($dependencies | Where-Object { $_.ConstructorArguments[0].Value -match 'BugfixesAndQoL|SpectatorEditorBuildTest' }).Count) { throw 'Migration dependencies are not permitted.' }
    # The controller fixture classifies every GameActionCommand of its enum double for the observer
    # command policy. That proof only holds while the double mirrors the installed game enum exactly;
    # a new or changed game value must fail here and be audited (exact names/values are the contract).
    $gameCommands = @($game.MainModule.GetType('Enums/GameActionCommand').Fields | Where-Object { $_.IsLiteral } | ForEach-Object { '{0}={1}' -f $_.Name, $_.Constant } | Sort-Object)
    $fixture = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'SpectatorDefeatRuntimeTests.cs'))
    $double = [regex]::Match($fixture, '(?s)public enum GameActionCommand\s*\{(.*?)\}').Groups[1].Value
    $fixtureCommands = @([regex]::Matches($double, '(\w+)\s*=\s*(\d+)') | ForEach-Object { '{0}={1}' -f $_.Groups[1].Value, $_.Groups[2].Value } | Sort-Object)
    if ($gameCommands.Count -eq 0 -or (Compare-Object $gameCommands $fixtureCommands)) { throw 'GameActionCommand fixture double differs from the installed game enum; re-audit the observer command policy.' }
    Write-Host 'PASS: compiled settings roles, shared preset bindings, tool bindings/locales and independent plugin dependencies.'
} finally { $mod.Dispose(); $api.Dispose(); $game.Dispose() }
