[CmdletBinding()]
param(
    [switch]$RunTests,
    [string]$ApiSharedDll = '',
    [string]$ExtenderDir = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins\000shcdese'
)
$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$roots = @((Join-Path $workspace 'APIShared'), $PSScriptRoot)
$files = @(foreach ($root in $roots) {
    Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object {
        $_.FullName -notmatch '\\(?:bin|obj|BepInEx|tests)\\' -and
        $_.Name -ne 'README.md' -and $_.Name -notlike '*.Tests.csproj' -and
        $_.Extension -in @('.cs','.csproj','.ps1','.bat','.json','.md','.xaml')
    }
})
$linked = @('Shared\NativePatternResolver.cs','Shared\DebugLogHelper.cs','Shared\DependencyFreeJson.cs')
$files += @($linked | ForEach-Object { Get-Item -LiteralPath (Join-Path $workspace $_) })
foreach ($file in $files) {
    $text = [IO.File]::ReadAllText($file.FullName)
    if ($text -match '(?<!\r)\n') { throw "Non-CRLF: $($file.FullName)" }
    if ($file.Extension -in @('.cs','.csproj') -and $text -match 'System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility|System\.Runtime\.Serialization\.Json') {
        throw "Forbidden runtime JSON: $($file.FullName)"
    }
    if ($file.Extension -eq '.cs') {
        if ($text -match '\b(?:void|IEnumerator)\s+(?:OnDestroy|OnDisable|OnApplicationQuit|OnApplicationPause|OnEnable|Start|Update|LateUpdate|FixedUpdate)\s*\(' -or
            $text -match '\b(?:StartCoroutine|InvokeRepeating|StopAllCoroutines)\s*\(') {
            throw "Lifecycle/long-lived MonoBehaviour callback: $($file.FullName)"
        }
        if ($file.FullName.StartsWith((Join-Path $PSScriptRoot 'src'), [StringComparison]::OrdinalIgnoreCase)) {
            if ($text -match '\.(?:Enable|Disable|DisableAll|Undo)\s*\(|\b(?:VirtualProtect|CodePatch\.Write|Marshal\.Write\w*|FlushInstructionCache)\s*\(') {
                throw "Executable runtime mutation/toggle: $($file.FullName)"
            }
            if ($text -match '\.Dispose\s*\(' -and
                ($file.Name -ne 'CaptureRuntime.cs' -or
                 ([regex]::Matches($text,'\.Dispose\s*\(')).Count -ne 1 -or
                 -not $text.Contains('if (!published) transaction?.Dispose();'))) {
                throw "Reachable published-hook teardown: $($file.FullName)"
            }
        }
    }
    if ($file.Extension -eq '.xaml') {
        [xml]$xml = $text
        foreach ($content in @($xml.SelectNodes("//*[local-name()='Content']"))) {
            if (@($content.ChildNodes | Where-Object NodeType -eq 'Element').Count -ne 1) { throw "XAML root count: $($file.FullName)" }
        }
    }
}
$runtime = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src\CaptureRuntime.cs'))
$plugin = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src\GatehouseLivingCaptureTestPlugin.cs'))
foreach ($required in @('private static CaptureRuntime runtime;', 'LibraryLoaded += OnLibraryLoaded;')) {
    if (-not $plugin.Contains($required)) { throw "Missing persistent runtime root: $required" }
}
foreach ($required in @('private readonly CaptureCallback rootedCallback;', 'OwnsHooks = true', 'published = true;', 'Volatile.Write(ref active, 1);', 'GATEHOUSE_LIVING_CAPTURE_CONFIRMED', 'CaptureDecision.Apply(context, unit, true);')) {
    if (-not $runtime.Contains($required)) { throw "Missing runtime contract: $required" }
}
$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'info.json') -Raw | ConvertFrom-Json
if ($manifest.NetworkMode -ne 1 -or $manifest.Version -ne '0.1.0' -or $manifest.GUID -ne 'GatehouseLivingCaptureTest_Serp') { throw 'Manifest gameplay/version/GUID mismatch' }
# Only public SE interop/API members are newly used. No Assembly-CSharp access is introduced.
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
[void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $game 'BepInEx\core\Mono.Cecil.dll')))
if ($ApiSharedDll) {
    $api = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($ApiSharedDll)
    try {
        $access = @($api.MainModule.Types | Where-Object FullName -eq 'APIShared.UnitAccess')[0]
        foreach ($parameter in @('SHCDESE.Interop.GameUnit&','SHCDESE.Interop.GameUnit*')) {
            $method = @($access.Methods | Where-Object {
                $_.Name -eq 'IsReallyAlive' -and $_.IsPublic -and $_.IsStatic -and
                $_.ReturnType.FullName -eq 'System.Boolean' -and $_.Parameters.Count -eq 1 -and
                $_.Parameters[0].ParameterType.FullName -eq $parameter
            })
            if ($method.Count -ne 1) { throw "APIShared lacks IsReallyAlive($parameter); build/install APIShared first." }
        }
    } finally { $api.Dispose() }
}
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $ExtenderDir 'SHCDESE.dll'))
try {
    $unit = @($assembly.MainModule.Types | Where-Object FullName -eq 'SHCDESE.Interop.GameUnit')[0]
    foreach ($name in @('r_AliveState','N0000019A','r_CurrentHealth','r_ControllableForPlayerId','r_GlobalId','r_CurrentPositionTileId')) {
        $field = @($unit.Fields | Where-Object Name -ceq $name)
        if ($field.Count -ne 1 -or -not $field[0].IsPublic) { throw "Installed private/missing GameUnit.$name" }
        Write-Host ("Public installed member: GameUnit." + $name + ' : ' + $field[0].FieldType.FullName)
    }
    $enum = @($assembly.MainModule.Types | Where-Object FullName -eq 'SHCDESE.Interop.Enums.AliveState')[0]
    $alive = @($enum.Fields | Where-Object Name -ceq 'IsAlive')[0]
    if ($alive.Constant -ne 2) { throw 'Installed AliveState.IsAlive differs from the native comparison' }
} finally { $assembly.Dispose() }
& (Join-Path $workspace 'Shared\Test-PermanentNativeRuntimePatches.ps1')
if (-not $?) { throw 'Workspace permanent-hook regression failed' }
Write-Host 'PASS: APIShared and new testmod JSON, lifecycle, persistent roots, permanent-hook contracts, CRLF and XAML checked.'
if ($RunTests) {
    $msbuild = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
    & $msbuild (Join-Path $PSScriptRoot 'GatehouseLivingCaptureTest.Tests.csproj') /t:Rebuild /p:Configuration=Debug "/p:ExtenderDir=$ExtenderDir" /nologo /verbosity:minimal
    if ($LASTEXITCODE -ne 0) { throw 'Capture test project failed to compile' }
    & (Join-Path $PSScriptRoot 'tests\bin\GatehouseLivingCaptureTest.Tests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Capture machine/life/native tests failed' }
}
