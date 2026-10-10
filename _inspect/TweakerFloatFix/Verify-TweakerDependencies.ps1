param([Parameter(Mandatory)][string]$Root)
$ErrorActionPreference = 'Stop'
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$source = [IO.File]::ReadAllText((Join-Path $Root 'Plugin.cs'))
$manifest = [IO.File]::ReadAllText((Join-Path $Root 'info.json')) | ConvertFrom-Json
# The workspace checker assumes local constants and MinimumScriptExtenderVersion.
# Tweaker uses a public external constant and the generic Dependencies array.
# Read the external constant from the actual compilation/runtime dependency.
$decompiled = (& ilspycmd -t SHCDESE.BepInEx.Bootstrap.Plugin (Join-Path $game 'BepInEx\plugins\000shcdese\SHCDESE.dll')) -join "`n"
if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect installed Extender metadata' }
$constant = [regex]::Match($decompiled, 'public const string PLUGIN_GUID = "([^"]+)";')
if (!$constant.Success) { throw 'Public Extender GUID constant not found' }
$declarations = [regex]::Matches($source, '\[BepInDependency\((SHCDESE\.BepInEx\.Bootstrap\.Plugin\.PLUGIN_GUID|"[^"]+"),\s*"([^"]+)"\)\]')
if ($declarations.Count -ne @($manifest.Dependencies).Count) { throw 'Dependency declaration count mismatch' }
foreach ($declaration in $declarations) {
    $token = $declaration.Groups[1].Value
    $guid = if ($token.StartsWith('"')) { $token.Trim('"') } else { $constant.Groups[1].Value }
    $matches = @($manifest.Dependencies | Where-Object GUID -CEQ $guid)
    if ($matches.Count -ne 1 -or $matches[0].MinimumVersion -cne $declaration.Groups[2].Value) {
        throw "Actual dependency declaration differs from manifest: $guid"
    }
    Write-Output "PASS: actual dependency $guid, minimum $($matches[0].MinimumVersion)"
}
