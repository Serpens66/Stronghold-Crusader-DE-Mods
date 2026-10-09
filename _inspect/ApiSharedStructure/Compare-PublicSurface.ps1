$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$zip=[IO.Compression.ZipFile]::OpenRead((Join-Path $workspace '_inspect/ApiSharedSubmodule/workspace-api-before.zip'))
try {
    $entry=@($zip.Entries | Where-Object { $_.FullName.EndsWith('BepInEx/plugins/APIShared_Serp/APIShared.dll') -or $_.FullName.EndsWith('BepInEx\plugins\APIShared_Serp\APIShared.dll') })
    if($entry.Count -ne 1){throw 'Expected one archived APIShared assembly'}
    $before=Join-Path $PSScriptRoot 'before-APIShared.dll'
    [IO.Compression.ZipFileExtensions]::ExtractToFile($entry[0],$before,$true)
} finally {$zip.Dispose()}
[void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes('E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\core\Mono.Cecil.dll'))
function Public-Surface([string]$Path) {
    $assembly=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($Path)
    function Types($Items){foreach($type in $Items){$type; Types $type.NestedTypes}}
    try {
        foreach($type in (Types $assembly.MainModule.Types) | Where-Object { $_.IsPublic -or $_.IsNestedPublic }) {
            'type|'+$type.FullName+'|'+$type.Attributes+'|'+$type.BaseType
            foreach($iface in $type.Interfaces){'interface|'+$type.FullName+'|'+$iface.InterfaceType.FullName}
            foreach($field in $type.Fields | Where-Object IsPublic){'field|'+$field.FullName+'|'+$field.Attributes+'|'+$field.Constant}
            foreach($method in $type.Methods | Where-Object { $_.IsPublic -or $_.IsFamily -or $_.IsFamilyOrAssembly }) {
                'method|'+$method.FullName+'|'+$method.Attributes
                foreach($parameter in $method.Parameters){'parameter|'+$method.FullName+'|'+$parameter.Name+'|'+$parameter.Attributes+'|'+$parameter.Constant}
            }
        }
    } finally {$assembly.Dispose()}
}
$old=@(Public-Surface $before | Sort-Object)
$new=@(Public-Surface (Join-Path $workspace 'APIShared/BepInEx/plugins/APIShared_Serp/APIShared.dll') | Sort-Object)
$diff=@(Compare-Object $old $new)
if($diff.Count){$diff | Out-String | Write-Output;throw 'Public compiled API changed'}
Write-Output ('PASS: '+$old.Count+' compiled public API/parameter contracts unchanged against the archived pre-migration assembly.')
