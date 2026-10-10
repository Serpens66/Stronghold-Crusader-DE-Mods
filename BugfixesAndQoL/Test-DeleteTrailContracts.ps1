[CmdletBinding()]
param([string]$GameDir = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition')
$ErrorActionPreference = 'Stop'
[Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $GameDir 'BepInEx\core\Mono.Cecil.dll'))) | Out-Null
$managed = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GameDir 'Stronghold Crusader Definitive Edition_Data\Managed\Assembly-CSharp.dll'))
try {
    foreach ($entry in @(
        @('ConfigSettings', 'GetUserCustomTrailsPath', 0),
        @('MapFileManager', 'GetCustomTrails', 1),
        @('MapFileManager', 'RescanCustomTrailsFolder', 0),
        @('CrusaderDE.HUD_ConfirmationPopup', 'ShowConfirmationMessage', 6),
        @('CrusaderDE.HUD_ConfirmationPopup', 'ShowConfirmationOKMessage', 4),
        @('CrusaderDE.FrontendMenus', 'UpdateFrontMenuPopupScale', 0))) {
        $type = @($managed.MainModule.Types | Where-Object FullName -CEQ $entry[0])
        $method = @($type[0].Methods | Where-Object { $_.Name -ceq $entry[1] -and $_.Parameters.Count -eq $entry[2] -and $_.IsPublic })
        if ($method.Count -ne 1) { throw ('Public Trail deletion contract changed: ' + ($entry -join '/')) }
        Write-Output $method[0].FullName
    }
    $manager = @($managed.MainModule.Types | Where-Object FullName -CEQ 'MapFileManager')[0]
    $info = @($manager.NestedTypes | Where-Object Name -CEQ 'CustomTrailInfo')[0]
    foreach ($entry in @(@('Name', 'System.String'), @('FullPath', 'System.String'), @('workshop', 'System.Boolean'))) {
        $field = @($info.Fields | Where-Object { $_.Name -ceq $entry[0] -and $_.IsPublic -and $_.FieldType.FullName -ceq $entry[1] })
        if ($field.Count -ne 1) { throw ('Public Trail metadata field changed: ' + $entry[0]) }
    }
    $row = @($managed.MainModule.Types | Where-Object FullName -CEQ 'CrusaderDE.FileRow')[0]
    if (@($row.Fields | Where-Object { $_.Name -ceq 'trail' -and $_.IsPublic -and $_.FieldType.FullName -ceq 'MapFileManager/CustomTrailInfo' }).Count -ne 1) { throw 'FileRow.trail contract changed.' }
    $main = @($managed.MainModule.Types | Where-Object FullName -CEQ 'CrusaderDE.MainViewModel')[0]
    if (@($main.Fields | Where-Object { $_.Name -ceq 'FrontEndMenu' -and $_.IsPublic -and $_.FieldType.FullName -ceq 'CrusaderDE.FrontendMenus' }).Count -ne 1) { throw 'FrontEndMenu field contract changed.' }
    foreach ($entry in @(
        @('CrusaderDE.FRONT_ManageTrail', 'Instance'),
        @('CrusaderDE.MainViewModel', 'Instance'),
        @('CrusaderDE.MainViewModel', 'Show_HUD_Confirmation'),
        @('CrusaderDE.MainViewModel', 'Show_HUD_ConfirmationMP'),
        @('MapFileManager', 'Instance'),
        @('CrusaderDE.FileRow', 'Text1'))) {
        $type = @($managed.MainModule.Types | Where-Object FullName -CEQ $entry[0])[0]
        $property = @($type.Properties | Where-Object Name -CEQ $entry[1])
        if ($property.Count -ne 1 -or -not $property[0].GetMethod.IsPublic) { throw ('Public property changed: ' + ($entry -join '.')) }
        if ($entry[1] -like 'Show_HUD*' -and -not $property[0].SetMethod.IsPublic) { throw 'Popup visibility setter is private.' }
    }
} finally { $managed.Dispose() }
Write-Output 'Trail deletion real managed-assembly contracts passed.'
