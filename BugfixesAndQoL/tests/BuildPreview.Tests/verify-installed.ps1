[CmdletBinding()]
param([string]$GameDir = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition')
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
. (Join-Path $workspace 'Shared\Tools\Release\Release.Common.ps1')
$local = Join-Path $workspace 'BugfixesAndQoL\BepInEx\plugins\BugfixesAndQoL_Serp'
$installed = Join-Path $GameDir 'BepInEx\plugins\BugfixesAndQoL_Serp'
$checked = 0
foreach ($file in Get-ChildItem -LiteralPath $local -Recurse -File) {
    $relative = $file.FullName.Substring($local.Length + 1)
    $target = Join-Path $installed $relative
    if (-not (Test-Path -LiteralPath $target -PathType Leaf) -or
        (Get-Sha256Hex -Path $file.FullName) -ine (Get-Sha256Hex -Path $target)) { throw "Installed file mismatch: $relative" }
    $checked++
}
$metadata = Get-Content -Raw -LiteralPath (Join-Path $installed 'info.json') | ConvertFrom-Json
if ($metadata.GUID -ne 'BugfixesAndQoL_Serp' -or $metadata.Version -ne '1.0.190') { throw 'Installed manifest version differs.' }
if (@($metadata.Dependencies | Where-Object { $_.GUID -eq 'APIShared_Serp' -and $_.MinimumVersion -eq '0.9.0' }).Count -ne 1) { throw 'Installed dependency differs.' }
$api = Join-Path $GameDir 'BepInEx\plugins\APIShared_Serp\APIShared.dll'
if ([Reflection.AssemblyName]::GetAssemblyName($api).Version -ne [version]'0.9.0.0') { throw 'Installed APIShared version differs.' }
if ((Get-Sha256Hex -Path $api) -ine (Get-Sha256Hex -Path (Join-Path $workspace 'APIShared\BepInEx\plugins\APIShared_Serp\APIShared.dll'))) { throw 'Installed APIShared differs.' }
[Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $GameDir 'BepInEx\core\Mono.Cecil.dll'))) | Out-Null
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $installed 'BugfixesAndQoL.dll'))
try {
    if ($assembly.Name.Version -ne [version]'1.0.190.0') { throw 'QoL assembly version differs.' }
    $runtime = $assembly.MainModule.GetType('BugfixesAndQoL.BuildPreview.BuildPreviewRuntime')
    if ($null -eq $runtime) { throw 'Integrated runtime missing.' }
    foreach ($name in @('State','settings','persistentLog','permanentHook','callback')) {
        if (@($runtime.Fields | Where-Object { $_.Name -eq $name -and $_.IsStatic }).Count -ne 1) { throw "Process root missing: $name" }
    }
    $preview = @($runtime.Methods | Where-Object Name -eq 'Preview')[0]
    $instructions = @($preview.Body.Instructions)
    $original = @($instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq 'get_Original' })
    if ($original.Count -ne 1) { throw 'Compiled Vanilla call count differs.' }
    # No closures, formatting or object construction can move ahead of the activation check.
    $firstOriginal = [Array]::IndexOf($instructions, $original[0])
    $prefix = @($instructions | Select-Object -First $firstOriginal)
    if (@($prefix | Where-Object { $_.OpCode.Name -eq 'newobj' }).Count -ne 0) { throw 'Optional allocation before Vanilla/activation.' }
    $model = $assembly.MainModule.GetType('BugfixesAndQoL.BugfixesAndQoLViewModel')
    $setting = @($model.Properties | Where-Object Name -eq 'EnableImprovedBuildPreview')[0]
    if (@($setting.CustomAttributes | Where-Object { $_.AttributeType.FullName -eq 'APIShared.ModSettings.PresetLocalAttribute' }).Count -ne 1 -or
        @($setting.CustomAttributes | Where-Object { $_.AttributeType.Name -match '^Sync' }).Count -ne 0) { throw 'Compiled local-only attribute differs.' }
    if (@($assembly.MainModule.AssemblyReferences | Where-Object { $_.Name -eq 'APIShared' -and $_.Version -eq [version]'0.9.0.0' }).Count -ne 1) { throw 'Compiled APIShared contract differs.' }
}
finally { $assembly.Dispose() }
Write-Host "PASS: $checked installed package file hashes; QoL 1.0.190/APIShared 0.9.0, static roots, one Vanilla call, no hoisted hot-path allocation and local-only compiled setting."
