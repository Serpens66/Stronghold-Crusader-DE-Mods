[CmdletBinding()]
param([string]$ApiSharedDirectory = $env:SHCDE_API_SHARED_DIR)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
. (Join-Path $workspace 'Shared\Tools\Release\Release.Common.ps1')
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$extender = Join-Path $game 'BepInEx\plugins\000shcdese'
$python = 'D:\CDesktopLink\Portable\Python\WinPy64\python\python.exe'
Write-Host ('[{0:yyyy-MM-dd HH:mm:ss.fff}] CrenelatedWallTest preflight starts' -f [DateTime]::Now)
if (-not $ApiSharedDirectory) { $ApiSharedDirectory = Join-Path $workspace 'APIShared\BepInEx\plugins\APIShared_Serp' }
$ApiSharedDirectory = [IO.Path]::GetFullPath($ApiSharedDirectory)
& (Join-Path $PSScriptRoot 'verify-api.ps1') -ApiSharedDirectory $ApiSharedDirectory
if (-not $?) { throw 'Selected dependency verification failed.' }
$native = Join-Path $game 'Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll'
if ((Get-Sha256Hex $native) -cne 'fbcb93195fc7efca9bdac5204852efdd76f9818f59a6711750d77c9cef2831e2') { throw 'Unknown native build; this testmod is hash-bound.' }
# The installed SE's public signatures are compiled below; audit-native validates
# the specific build-event semantics and native contracts used by this mod.
$files = @(Get-ChildItem -LiteralPath $PSScriptRoot -Recurse -File | Where-Object {
    $_.FullName -notmatch '[\\/](bin|obj|BepInEx)[\\/]' -and $_.Extension -in @('.cs','.csproj','.ps1','.bat','.json','.md','.py','.xaml','.txt')
})
foreach ($file in $files) {
    $body = [IO.File]::ReadAllText($file.FullName)
    if ($body -match '(?<!\r)\n') { throw "Bare LF: $($file.FullName)" }
    if ($file.Extension -notin @('.py','.ps1') -and $body.Contains('\r\n')) { throw "Literal backslash CRLF: $($file.FullName)" }
    if ($file.FullName -notmatch '[\\/]tests[\\/]' -and $file.Extension -in @('.cs','.csproj')) {
        if ($body -match 'System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility|Serialization\.Json|Assembly-CSharp-publicized') { throw "Forbidden JSON/publicized reference: $($file.Name)" }
        if ($body -match '\b(?:OnDestroy|OnDisable|OnApplicationQuit|Update|LateUpdate|FixedUpdate|OnGUI|OnRenderObject|OnPostRender|StartCoroutine|InvokeRepeating)\s*\(') { throw "Forbidden runtime lifecycle/scheduling: $($file.Name)" }
        if ($body -match 'VirtualProtect|CodePatch\.Write|\.Undo\s*\(|\.Unpatch|\.Disable\s*\(') { throw "Unexpected native mutation/teardown: $($file.Name)" }
        # This one data write is the feature's audited native output, never executable memory.
        $withoutStatusWrite = $body.Replace('Marshal.WriteInt32(manager, InvalidOffset, 1);', '')
        if ($withoutStatusWrite -match 'Marshal\.Write' -or ($file.Name -ne 'PreviewValidationHook.cs' -and $body -match 'Marshal\.Write')) { throw "Unexpected native data write: $($file.Name)" }
        if ($file.Name -eq 'StraightGraphicsHook.cs') {
            if ([regex]::Matches($body, 'hook\.Enable\s*\(').Count -ne 1 -or [regex]::Matches($body, '\.Dispose\s*\(').Count -ne 1 -or
                -not $body.Contains('catch { hook.Dispose(); hook = null; throw; }') -or
                -not $body.Contains('try { Validate(hook, unchecked((ulong)target.ToInt64())); }')) { throw 'Graphics hook preparation/publication lifetime changed.' }
        } elseif ($file.Name -eq 'PreviewValidationHook.cs') {
            # Limited structural lifetime guardrail, complemented by manual review and backend fixtures.
            if ([regex]::Matches($body, 'hook\.Enable\s*\(').Count -ne 1 -or
                [regex]::Matches($body, '\.Dispose\s*\(').Count -ne 1 -or
                -not $body.Contains('if (!published)') -or -not $body.Contains('published = true;')) { throw 'Preview hook lifetime requires review.' }
        } elseif ($body -match 'NativeDetour|HookTransaction|\.Dispose\s*\(|\.(?:Enable|Apply)\s*\(') { throw "Unexpected native hook: $($file.Name)" }
    }
}
# Inspect the installed enum declarations directly; no Unity private members are used.
$seAssembly = [Reflection.Assembly]::LoadFrom((Join-Path $extender 'SHCDESE.dll'))
foreach ($item in @(
    @('SHCDESE.Interop.eMappers', 'MAPPER_CRENAL', 26),
    @('SHCDESE.Interop.eGoods', 'STORED_STONE_BLOCKS', 4),
    @('SHCDESE.Interop.Enums.TilePropertyFlag', 'IsWall', 256),
    @('SHCDESE.Interop.Enums.TilePropertyFlag', 'CrenelatedLow', 512),
    @('SHCDESE.Interop.Enums.TilePropertyFlag', 'CrenelatedHigh', 4194304)
)) {
    $type = $seAssembly.GetType($item[0], $true)
    if ([int]$type.GetField($item[1]).GetRawConstantValue() -ne $item[2]) { throw "Changed domain symbol: $($item[0]).$($item[1])" }
}
. (Join-Path $workspace 'Shared\Tools\ScriptExtenderUpdate\ScriptExtenderUpdate.Common.ps1')
Assert-SERuntimeModPreflight ([pscustomobject]@{ Name='CrenelatedWallTest'; Project='Testmods\CrenelatedWallTest\CrenelatedWallTest.csproj'; Plugin='Testmods\CrenelatedWallTest\src\Plugin.cs' }) $workspace
& {
    # The existing dependency validator treats absent optional manifest properties as
    # null. Do not leak Release.Common's StrictMode into that independent validator.
    Set-StrictMode -Off
    & (Join-Path $workspace 'Shared\Tools\Validation\Test-SharedBoundaries.ps1') -Workspace $workspace -ModDirectory 'Testmods\CrenelatedWallTest' -ConsumerOnly
}
if (-not $?) { throw 'Shared/dependency/tooltip/permanent-hook regression failed.' }
[xml]$xaml = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Override\ScriptExtenderUI\CrenelatedWallTestSettings.xaml'))
foreach ($node in $xaml.SelectNodes("//*[local-name()='Button' or local-name()='CheckBox' or local-name()='ComboBox' or local-name()='TextBox']")) {
    if (-not $node.GetAttribute('ToolTip') -or $node.GetAttribute('ToolTipService.ShowDuration') -ne '60000') { throw 'Missing 60000-ms tooltip.' }
}
$viewer = $xaml.SelectSingleNode("//*[local-name()='ScrollViewer']")
if ($viewer.GetAttribute('HorizontalScrollBarVisibility') -ne 'Auto' -or $viewer.GetAttribute('VerticalScrollBarVisibility') -ne 'Auto') { throw 'Settings scroll contract changed.' }
$keySignature = $null
foreach ($locale in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Locales') -File -Filter '*.txt') {
    $keys = @(Get-Content -LiteralPath $locale.FullName | Where-Object { $_ -match '^[^=]+=' } | ForEach-Object { $_.Split('=')[0] } | Sort-Object)
    $signature = $keys -join '|'
    if ($null -eq $keySignature) { $keySignature = $signature } elseif ($signature -cne $keySignature) { throw "Locale key mismatch: $($locale.Name)" }
}
& $python (Join-Path $PSScriptRoot 'tests\audit-native.py') $workspace
if ($LASTEXITCODE -ne 0) { throw 'Native feature regression failed.' }
& dotnet build (Join-Path $PSScriptRoot 'tests\CrenelatedWallTest.Tests.csproj') --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw 'Tests compilation failed.' }
& dotnet (Join-Path $PSScriptRoot 'tests\bin\Debug\net10.0\CrenelatedWallTest.Tests.dll') $workspace $ApiSharedDirectory
if ($LASTEXITCODE -ne 0) { throw 'Production signatures/policy/persistence regression failed.' }
& dotnet msbuild (Join-Path $PSScriptRoot 'tests\NativeHookTests.csproj') /t:Build /verbosity:minimal ("/p:ApiSharedDir=" + $ApiSharedDirectory)
if ($LASTEXITCODE -ne 0) { throw 'Native hook fixture compilation failed.' }
& (Join-Path $PSScriptRoot 'tests\bin\NativeHookTests\CrenelatedWallTest.NativeHookTests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Actual installed NativeX64 hook/trampoline fixture failed.' }
# No settings/preset behavior changes in this SDK-reference update: compile every
# real binding against the selected SDK above instead of running unrelated mods.
Write-Host ('[{0:yyyy-MM-dd HH:mm:ss.fff}] PASS: native audit, selected SDK compatibility, CRLF/JSON/lifecycle, scoped dependencies, XAML/locales, wall policy, save codec and NativeX64 fixtures.' -f [DateTime]::Now)
