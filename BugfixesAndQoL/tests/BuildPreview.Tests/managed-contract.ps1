[CmdletBinding()]
param([Parameter(Mandatory)][string]$GameDir)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\..\..\Shared\Tools\Release\Release.Common.ps1')
$managed = Join-Path $GameDir 'Stronghold Crusader Definitive Edition_Data\Managed\Assembly-CSharp.dll'
if ((Get-Sha256Hex -Path $managed) -ine 'BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789') { throw 'Unreviewed managed game assembly.' }
[Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $GameDir 'BepInEx\core\Mono.Cecil.dll'))) | Out-Null
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($managed)
try {
    $fields = @(
        @('CrusaderDE.HUD_Main','HoverStruct','System.Int32',$false,$false),
        @('CrusaderDE.HUD_Main','SelectedStruct','System.Int32',$false,$false),
        @('CrusaderDE.HUD_Main','HoverString','System.String',$true,$false),
        @('CrusaderDE.HUD_Main','SelectedString','System.String',$false,$false),
        @('CrusaderDE.HUD_Main','OtherVisible','System.Boolean',$false,$false),
        @('CrusaderDE.HUD_Main','OtherHighestPri','System.Boolean',$false,$false),
        @('CrusaderDE.MainViewModel','HUDmain','CrusaderDE.HUD_Main',$true,$false),
        @('MainControls','instance','MainControls',$true,$true),
        @('MainControls','overGUI','System.Boolean',$true,$false),
        @('GameMap','instance','GameMap',$true,$true),
        @('GameMap','pendingRotationtriggered','System.Boolean',$true,$false),
        @('GameMapTile','gameMapX','System.Int32',$true,$false),
        @('GameMapTile','gameMapY','System.Int32',$true,$false)
    )
    foreach ($row in $fields) {
        $type = $assembly.MainModule.GetType($row[0]); $field = @($type.Fields | Where-Object Name -eq $row[1])
        if ($field.Count -ne 1 -or $field[0].FieldType.FullName -ne $row[2] -or $field[0].IsPublic -ne $row[3] -or $field[0].IsStatic -ne $row[4]) { throw "Real field differs: $($row[0]).$($row[1])" }
    }
    $properties = @(
        @('CrusaderDE.MainViewModel','Instance','CrusaderDE.MainViewModel',$true),
        @('CrusaderDE.MainViewModel','RolloverBuilding_TooltipVis','System.Boolean',$false),
        @('MainControls','CurrentAction','System.Int32',$false),
        @('MainControls','CurrentSubAction','System.Int32',$false)
    )
    foreach ($row in $properties) {
        $type = $assembly.MainModule.GetType($row[0]); $property = @($type.Properties | Where-Object Name -eq $row[1])
        if ($property.Count -ne 1 -or $property[0].PropertyType.FullName -ne $row[2] -or -not $property[0].GetMethod.IsPublic -or $property[0].GetMethod.IsStatic -ne $row[3]) { throw "Real property differs: $($row[0]).$($row[1])" }
    }
    $methods = @(
        @('MainControls','isOffWorld','System.Boolean',''),
        @('MainControls','getMouseMapTilePosition','System.Void','System.Single&,System.Single&'),
        @('GameMap','getMapTile','GameMapTile','System.Int32,System.Int32')
    )
    foreach ($row in $methods) {
        $type = $assembly.MainModule.GetType($row[0])
        $methodsFound = @($type.Methods | Where-Object { $_.Name -eq $row[1] -and $_.IsPublic -and -not $_.IsStatic -and $_.ReturnType.FullName -eq $row[2] -and (($_.Parameters | ForEach-Object { $_.ParameterType.FullName }) -join ',') -eq $row[3] })
        if ($methodsFound.Count -ne 1) { throw "Real method differs: $($row[0]).$($row[1])" }
    }
    Write-Host 'PASS: real managed hash; 13 fields, four getters, three exact method signatures (no publicized references).'
} finally { $assembly.Dispose() }
