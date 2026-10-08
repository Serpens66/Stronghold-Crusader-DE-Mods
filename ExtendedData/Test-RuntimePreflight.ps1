[CmdletBinding()]
param(
    [string]$GameDir = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
)

$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $workspace 'Shared\Tools\ScriptExtenderUpdate\ScriptExtenderUpdate.Common.ps1')

$mods = Get-Content -Raw -LiteralPath (Join-Path $workspace 'Shared\Tools\ScriptExtenderUpdate\mods.json') | ConvertFrom-Json
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
    $platformType = $managedAssembly.MainModule.Types | Where-Object { $_.FullName -ceq 'Platform_Multiplayer' }
    $memberType = $platformType.NestedTypes | Where-Object { $_.Name -ceq 'MPLobbyMember' }
    $combinedNameGetter = @($memberType.Methods | Where-Object {
        $_.Name -ceq 'get_CombinedName' -and $_.Parameters.Count -eq 0
    })
    if ($combinedNameGetter.Count -ne 1 -or -not $combinedNameGetter[0].IsPublic -or
        $combinedNameGetter[0].IsStatic -or
        $combinedNameGetter[0].ReturnType.FullName -cne 'System.String') {
        throw 'Managed contract changed for MPLobbyMember.get_CombinedName.'
    }
    foreach ($pageNumber in 1..4) {
        $page = $managedAssembly.MainModule.Types | Where-Object {
            $_.FullName -ceq "CrusaderDE.FRONT_CoopTrail$pageNumber"
        }
        $row = $page.NestedTypes | Where-Object { $_.Name -ceq 'PlayerRow' }
        $method = @($row.Methods | Where-Object {
            $_.Name -ceq 'Update' -and $_.Parameters.Count -eq 4 -and
            $_.Parameters[0].ParameterType.FullName -ceq 'CrusaderDE.FRONT_Multiplayer' -and
            $_.Parameters[1].ParameterType.FullName -ceq 'Platform_Multiplayer/MPLobbyMember' -and
            $_.Parameters[2].ParameterType.FullName -ceq 'System.Int32' -and
            $_.Parameters[3].ParameterType.FullName -ceq 'System.Int32'
        })
        $rowField = @($row.Fields | Where-Object { $_.Name -ceq 'RefRow' })
        $pageField = @($page.Fields | Where-Object { $_.Name -ceq 'playerRows' })
        if ($null -eq $page -or $null -eq $row -or $method.Count -ne 1 -or
            -not $method[0].IsPublic -or $method[0].IsStatic -or
            $method[0].ReturnType.FullName -cne 'System.Void' -or
            $rowField.Count -ne 1 -or -not $rowField[0].IsPublic -or
            $rowField[0].FieldType.FullName -cne 'Noesis.Grid' -or
            $pageField.Count -ne 1 -or -not $pageField[0].IsPublic) {
            throw "Managed Coop preview row contract changed on page $pageNumber."
        }
    }
    $lobbyType = $platformType.NestedTypes | Where-Object { $_.Name -ceq 'MPLobby' }
    $getTeam = @($lobbyType.Methods | Where-Object {
        $_.Name -ceq 'getTeam' -and $_.Parameters.Count -eq 1 -and
        $_.Parameters[0].ParameterType.FullName -ceq 'Platform_Multiplayer/MPLobbyMember'
    })
    if ($getTeam.Count -ne 1 -or -not $getTeam[0].IsPublic -or
        $getTeam[0].ReturnType.FullName -cne 'System.Int32') {
        throw 'Managed lobby team lookup contract changed.'
    }
    Assert-ManagedMethodContract $managedAssembly 'CrusaderDE.FRONT_Multiplayer' 'LeaveLobby' @(
        'System.Boolean', 'System.Boolean') 'Private'
    Assert-ManagedMethodContract $managedAssembly 'CrusaderDE.FRONT_Multiplayer' 'StartSkirmishGame' @(
        'CrusaderDE.HUD_IngameMenu/RestartSkirmishMapInfo') 'Private'
    Assert-ManagedMethodContract $managedAssembly 'CrusaderDE.FRONT_Multiplayer' 'UpdateHostInfo' @(
        'System.Boolean') 'Private'
    Assert-ManagedMethodContract $managedAssembly 'CrusaderDE.FRONT_Multiplayer' 'UpdateRadarShieldPositions' @() 'Private'
    Assert-ManagedMethodContract $managedAssembly 'CrusaderDE.FRONT_Multiplayer' 'ReSortTeamInfo' @() 'Private'
    Assert-ManagedMethodContract $managedAssembly 'CrusaderDE.FRONT_Multiplayer' 'UpdateCustomLordNamesFromMP' @() 'Public'
    Assert-ManagedMethodContract $managedAssembly 'CrusaderDE.FrontendMenus' 'GenerateSwords' @() 'Public'
    Assert-ManagedMethodContract $managedAssembly 'CrusaderDE.FrontendMenus' 'ButtonTrailCampaignClicked' @(
        'System.Int32', 'System.Boolean') 'Public'
    Assert-ManagedMethodContract $managedAssembly 'CrusaderDE.MainViewModel' 'SetTrailSwordImage' @(
        'System.Int32', 'Noesis.ImageSource') 'Public'
    foreach ($contract in @(
        @('CrusaderDE.MainViewModel', 'HUDIngameMenu', 'CrusaderDE.HUD_IngameMenu'),
        @('CrusaderDE.HUD_IngameMenu', 'restartSkirmishMapInfo', 'CrusaderDE.HUD_IngameMenu/RestartSkirmishMapInfo'),
        @('CrusaderDE.HUD_IngameMenu/RestartSkirmishMapInfo', 'aivs', 'CrusaderDE.FRONT_Multiplayer/MPAIVInfo[]'),
        @('CrusaderDE.FRONT_Multiplayer/MPAIVInfo', 'builtInLord', 'System.Boolean'),
        @('CrusaderDE.FRONT_Multiplayer/MPAIVInfo', 'lordConfig', 'CustomisationFileManager/CustomLordConfig'),
        @('CustomisationFileManager/CustomLordConfig', 'lordType', 'System.Int32'),
        @('CustomisationFileManager/CustomLordConfig', 'name', 'System.String'),
        @('CustomisationFileManager/CustomLordConfig', 'path', 'System.String'),
        @('CrusaderDE.FRONT_Multiplayer/MPAIVInfo', 'lordType', 'System.Int32'),
        @('CrusaderDE.FRONT_Multiplayer/MPAIVInfo', 'lordName', 'System.String'),
        @('ConfigSettings', 'extendedLordPaths', 'System.String[]'),
        @('CrusaderDE.MainViewModel', 'FrontEndMenu', 'CrusaderDE.FrontendMenus'),
        @('CustomisationFileManager/CustomLordConfig', 'checksum', 'System.UInt64')
    )) {
        $parts = [string[]]$contract
        $allTypes = @($managedAssembly.MainModule.Types)
        foreach ($top in @($managedAssembly.MainModule.Types)) {
            $allTypes += @($top.NestedTypes)
        }
        $owner = $allTypes | Where-Object { $_.FullName -ceq $parts[0] }
        $field = @($owner.Fields | Where-Object { $_.Name -ceq $parts[1] })
        if ($null -eq $owner -or $field.Count -ne 1 -or -not $field[0].IsPublic -or
            $field[0].FieldType.FullName -cne $parts[2]) {
            throw "Managed single-player Lord selection field changed: $($parts[0]).$($parts[1])."
        }
    }
    $customisation = $managedAssembly.MainModule.Types |
        Where-Object { $_.FullName -ceq 'CustomisationFileManager' }
    $lookup = @($customisation.Methods | Where-Object {
        $_.Name -ceq 'getLordLordList' -and $_.IsPublic -and
        $_.Parameters.Count -eq 2 -and
        $_.Parameters[0].ParameterType.FullName -ceq 'System.Int32' -and
        $_.Parameters[1].ParameterType.FullName -ceq 'System.String'
    })
    if ($lookup.Count -ne 1) { throw 'Managed Custom Lord lookup contract changed.' }
    $viewModel = $managedAssembly.MainModule.Types | Where-Object FullName -CEQ 'CrusaderDE.MainViewModel'
    foreach ($name in @('Show_HUD_Confirmation', 'Show_HUD_ConfirmationMP')) {
        $property = @($viewModel.Properties | Where-Object Name -CEQ $name)
        if ($property.Count -ne 1 -or $property[0].PropertyType.FullName -cne 'System.Boolean' -or
            -not $property[0].SetMethod.IsPublic -or $property[0].SetMethod.IsStatic) {
            throw "Trail export popup property contract changed: $name"
        }
    }
    Assert-ManagedMethodContract $managedAssembly 'CrusaderDE.FrontendMenus' 'UpdateFrontMenuPopupScale' @() 'Public'
    Assert-ManagedMethodContract $managedAssembly 'EditorDirector' 'SaveSaveGameOrMap' @(
        'System.String', 'System.String', 'System.Boolean', 'System.Boolean', 'System.Boolean') 'Public'
}
finally {
    $managedAssembly.Dispose()
}

Write-Output 'ExtendedData runtime JSON/lifecycle preflight succeeded.'
