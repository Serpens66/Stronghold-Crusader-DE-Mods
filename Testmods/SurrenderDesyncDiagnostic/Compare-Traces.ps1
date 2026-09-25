[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$HostTrace,
    [Parameter(Mandatory=$true)][string]$ClientTrace,
    [switch]$Events
)

$ErrorActionPreference = 'Stop'
$gameDir = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
foreach ($dependency in @(
    (Join-Path $gameDir 'BepInEx\core\BepInEx.dll'),
    (Join-Path $gameDir 'BepInEx\plugins\000shcdese\System.Memory.dll'),
    (Join-Path $gameDir 'BepInEx\plugins\000shcdese\System.Buffers.dll'),
    (Join-Path $gameDir 'BepInEx\plugins\000shcdese\SHCDESE.dll'),
    (Join-Path $gameDir 'BepInEx\plugins\APIShared_Serp\APIShared.dll'),
    (Join-Path $gameDir 'BepInEx\plugins\BugfixesAndQoL_Serp\BugfixesAndQoL.dll')
)) { [void][Reflection.Assembly]::LoadFrom($dependency) }

$dll = Join-Path $PSScriptRoot 'BepInEx\plugins\SurrenderDesyncDiagnostic_Serp\SurrenderDesyncDiagnostic.dll'
$assembly = [Reflection.Assembly]::LoadFrom($dll)
$comparer = $assembly.GetType('SurrenderDesyncDiagnostic.BinaryTraceComparison', $true)
try {
    $comparer.GetMethod('Compare').Invoke($null, @($HostTrace, $ClientTrace))
    if ($Events) {
        'HOST_EVENTS'
        $comparer.GetMethod('Events').Invoke($null, @($HostTrace))
        'CLIENT_EVENTS'
        $comparer.GetMethod('Events').Invoke($null, @($ClientTrace))
    }
}
catch [Reflection.TargetInvocationException] {
    throw $_.Exception.InnerException
}
