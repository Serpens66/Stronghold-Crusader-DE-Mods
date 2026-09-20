[CmdletBinding()]
param(
    [string]$GameDir = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
)

$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $workspace 'Shared\ScriptExtenderUpdate\ScriptExtenderUpdate.Common.ps1')

$mods = Get-Content -Raw -LiteralPath (Join-Path $workspace 'Shared\ScriptExtenderUpdate\mods.json') | ConvertFrom-Json
# Windows PowerShell 5.1 preserves a top-level JSON array as one pipeline object,
# while newer PowerShell versions enumerate it. A foreach statement handles both forms.
$mod = @(foreach ($entry in $mods) {
    if ([string]$entry.Name -ceq 'ExtendedData') {
        $entry
    }
})
if ($mod.Count -ne 1) {
    throw "Expected exactly one ExtendedData Script Extender inventory entry; found $($mod.Count)."
}

Assert-SERuntimeModPreflight $mod[0] $workspace

$cecilPath = Join-Path $GameDir 'BepInEx\core\Mono.Cecil.dll'
$assemblyPath = Join-Path $GameDir 'Stronghold Crusader Definitive Edition_Data\Managed\Assembly-CSharp.dll'
if (-not (Test-Path -LiteralPath $cecilPath)) {
    throw "Mono.Cecil.dll was not found: $cecilPath"
}
if (-not (Test-Path -LiteralPath $assemblyPath)) {
    throw "Assembly-CSharp.dll was not found: $assemblyPath"
}
$cecilAssembly = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes($cecilPath))
if ($null -eq $cecilAssembly) {
    throw "Mono.Cecil.dll could not be loaded from: $cecilPath"
}

function Assert-ManagedMethodContract {
    param(
        [Mono.Cecil.AssemblyDefinition]$Assembly,
        [string]$TypeName,
        [string]$MethodName,
        [string[]]$ParameterTypes,
        [ValidateSet('Public', 'Private')]
        [string]$Visibility
    )

    $type = $Assembly.MainModule.Types | Where-Object { $_.FullName -ceq $TypeName }
    if ($null -eq $type) {
        throw "Managed contract type was not found: $TypeName"
    }
    $matches = @($type.Methods | Where-Object {
        if ($_.Name -cne $MethodName -or $_.Parameters.Count -ne $ParameterTypes.Count) {
            return $false
        }
        for ($index = 0; $index -lt $ParameterTypes.Count; $index++) {
            if ($_.Parameters[$index].ParameterType.FullName -cne $ParameterTypes[$index]) {
                return $false
            }
        }
        return $true
    })
    if ($matches.Count -ne 1) {
        throw "Expected exactly one managed contract method $TypeName.$MethodName; found $($matches.Count)."
    }
    $method = $matches[0]
    if ($method.IsStatic -or $method.ReturnType.FullName -cne 'System.Void') {
        throw "Managed contract must be an instance void method: $TypeName.$MethodName"
    }
    if (($Visibility -ceq 'Public' -and -not $method.IsPublic) -or
        ($Visibility -ceq 'Private' -and -not $method.IsPrivate)) {
        throw "Managed contract visibility changed for $TypeName.$MethodName; expected $Visibility."
    }
}

$managedAssembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($assemblyPath)
try {
    Assert-ManagedMethodContract $managedAssembly 'CrusaderDE.FRONT_Multiplayer' 'LeaveLobby' @(
        'System.Boolean', 'System.Boolean') 'Private'
    Assert-ManagedMethodContract $managedAssembly 'CrusaderDE.FRONT_Multiplayer' 'StartSkirmishGame' @(
        'CrusaderDE.HUD_IngameMenu/RestartSkirmishMapInfo') 'Private'
    Assert-ManagedMethodContract $managedAssembly 'CrusaderDE.FRONT_Multiplayer' 'UpdateHostInfo' @(
        'System.Boolean') 'Private'
    Assert-ManagedMethodContract $managedAssembly 'EditorDirector' 'SaveSaveGameOrMap' @(
        'System.String', 'System.String', 'System.Boolean', 'System.Boolean', 'System.Boolean') 'Public'
}
finally {
    $managedAssembly.Dispose()
}

Write-Output 'ExtendedData runtime JSON/lifecycle preflight succeeded.'
