param([switch]$SkipTests, [string]$GameDir, [string]$ExtenderDir, [string]$ApiSharedDir)
$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$runtimePath = Join-Path $PSScriptRoot 'src\AIKeepRangeRuntime.cs'
$runtime = [IO.File]::ReadAllText($runtimePath)
$contract = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src\AIKeepRangeNativeContract.cs'))
$decision = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src\AIKeepRangeDecision.cs'))
$source = $runtime + $contract + $decision
if ($contract -match 'AllocHGlobal|FreeHGlobal|VirtualFree|NativeMemoryManager.Free' -or
    $contract -notmatch 'ProbeBackend\(target, NativeMemoryManager.AllocateStub\)' -or
    $contract -notmatch 'AllowedSchemes = DetourScheme.Indirect' -or
    $contract -notmatch 'NativeMemoryManager.WriteStub\(copy, buffer\)' -or
    $runtime -notmatch 'AIKeepRangeNativeContract.Backend.CreateDetour') {
    throw 'Probe storage must use target-relative RedBird slabs and the same Indirect-only backend as the real hook.'
}
$vm = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src\BugfixesAndQoLViewModel.cs'))
$parent = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src\BugfixesAndQoLRuntime.cs'))
if ($source -match 'System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft|DataContractJsonSerializer|JsonUtility|threadLock|KeepProximityOverride|SetKeepProximityRange|CodePatch\.Write|Marshal\.Write|VirtualProtect|\.Undo\s*\(|\.Disable\s*\(') { throw 'Forbidden serializer/engine access/global override/executable mutation.' }
if ($source -match '\b(OnDestroy|OnDisable|OnApplicationQuit|OnApplicationPause|Update|LateUpdate|FixedUpdate|StartCoroutine)\s*\(') { throw 'Unexpected Unity lifecycle or polling.' }
if ($source -match '\bhook\??\.(Enable|Dispose)\s*\(' -or
    [regex]::Matches($source,'candidate\.Enable\(\)').Count -ne 2 -or
    [regex]::Matches($source,'candidate\?\.Dispose\(\)').Count -ne 2 -or
    $runtime -notmatch '(?s)catch\s*\{\s*candidate\?\.Dispose\(\);[^}]+throw;' -or
    $parent -notmatch 'private static AIKeepRangeRuntime processAIKeepRangeRuntime;' -or
    $parent -match 'processAIKeepRangeRuntime\??\.Dispose' -or
    $runtime -notmatch 'AIKeepRangeDecision.Execute\(bypass, original, manager, playerId, x, y, range\)') { throw 'Permanent hook lifetime or original forwarding changed.' }
if ($runtime.IndexOf('Chainloader.PluginInfos.ContainsKey("AIKeepRangeLimitTest_Serp")') -lt 0 -or
    $runtime.IndexOf('Chainloader.PluginInfos') -gt $runtime.IndexOf('AIKeepRangeNativeContract.Resolve')) { throw 'Legacy testmod must be rejected before resolving/patching.' }
if ([regex]::Matches($runtime,'DebugLogHelper.LogInfo\(').Count -ne 0 -or
    [regex]::Matches($runtime,'DebugLogHelper.LogDebug\(').Count -ne 2 -or
    $runtime -notmatch 'AI_KEEP_RANGE_READY:' -or
    $runtime -match 'Interlocked.Increment|AI exception:|human Vanilla forwarding:') { throw 'Unexpected verbose callback logging.' }
if ($vm -notmatch 'private bool removeAIKeepRangeLimit = true;' -or
    $vm -notmatch '\[SyncHostOnly\]\s*public bool RemoveAIKeepRangeLimit' -or
    $vm -notmatch 'RemoveAIKeepRangeLimit = true;' -or
    $vm -notmatch 'SetSetting\(ref removeAIKeepRangeLimit, value, nameof\(RemoveAIKeepRangeLimit\)\)' -or
    $parent -notmatch '(?s)if \(propertyName == nameof\(BugfixesAndQoLViewModel.RemoveAIKeepRangeLimit\)\)\s*\{\s*processAIKeepRangeRuntime\?\.Refresh\(\);\s*return;') { throw 'Shared host/default/reset/single-update contract.' }
[xml]$xaml = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'Override\ScriptExtenderUI\BugfixesAndQoLSettings.xaml')
$option = $xaml.SelectSingleNode('//*[local-name()="CheckBox" and @IsChecked="{Binding RemoveAIKeepRangeLimit, Mode=TwoWay}"]')
if ($null -eq $option -or $option.GetAttribute('ToolTipService.ShowDuration') -ne '60000' -or
    $null -eq $option.SelectSingleNode('ancestor::*[@IsEnabled="{Binding CanEditHostSettings}"]') -or
    $null -eq $option.SelectSingleNode('ancestor::*[@*[local-name()="ModSettingsSearch.SectionKey"]="bugfixes.section.host-possible-fixes"]')) { throw 'Fixes? host section/tooltip contract.' }
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Locales') -Filter '*.txt') {
    $text = [IO.File]::ReadAllText($file.FullName)
    foreach ($key in @('BugfixesAndQoL.RemoveAIKeepRangeLimit','BugfixesAndQoL.RemoveAIKeepRangeLimitHelp')) {
        if ([regex]::Matches($text,'(?m)^'+[regex]::Escape($key)+'=.+').Count -ne 1) { throw "Locale key: $($file.Name) $key" }
    }
}
$files = @('src\AIKeepRangeRuntime.cs','src\AIKeepRangeNativeContract.cs','src\AIKeepRangeDecision.cs','Test-AIKeepRangePreflight.ps1',
    'tests\AIKeepRange.Tests\Program.cs','tests\AIKeepRange.Tests\RuntimeHarness.cs','tests\AIKeepRange.Tests\AIKeepRange.Tests.csproj')
foreach ($file in $files) {
    $text=[IO.File]::ReadAllText((Join-Path $PSScriptRoot $file))
    if ($text -match '(?<!\r)\n|\r(?!\n)') { throw "Non-CRLF: $file" }
}
& (Join-Path $workspace 'Shared\Test-PermanentNativeRuntimePatches.ps1')
Write-Output 'PASS: AI keep range lifetime, migration guard, minimal logging, host settings, Fixes? XAML, locale parity and CRLF.'
if (-not $SkipTests) {
    & 'D:\CDesktopLink\Portable\Python\WinPy64\python\python.exe' (Join-Path $workspace '_inspect\AIKeepRangeLimit\audit.py')
    if ($LASTEXITCODE -ne 0) { throw 'Native identity/xref audit failed.' }
    $buildArguments = @('/t:Build', '/p:Configuration=Release', '/verbosity:minimal')
    foreach ($dependency in @(@('GameDir', $GameDir), @('ExtenderDir', $ExtenderDir), @('ApiSharedDir', $ApiSharedDir))) {
        if (-not [string]::IsNullOrWhiteSpace($dependency[1])) {
            $buildArguments += '/p:{0}={1}' -f $dependency[0], $dependency[1]
        }
    }
    & dotnet msbuild (Join-Path $PSScriptRoot 'tests\AIKeepRange.Tests\AIKeepRange.Tests.csproj') @buildArguments
    if ($LASTEXITCODE -ne 0) { throw 'AI keep range tests compilation failed.' }
    & (Join-Path $PSScriptRoot 'tests\AIKeepRange.Tests\bin\AIKeepRangeTests.exe') (Join-Path $workspace '_inspect\AIKeepRangeLimit\EEF90.bin')
    if ($LASTEXITCODE -ne 0) { throw 'AI keep range native/runtime tests failed.' }
}
