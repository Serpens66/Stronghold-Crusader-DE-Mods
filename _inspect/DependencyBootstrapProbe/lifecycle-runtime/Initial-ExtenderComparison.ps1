param([string]$GameDir='E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '../tools/ProbeContinuation.ps1')
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$prior=Join-Path $workspace '_inspect/DependencyBootstrapProbe/Correction-accepted-20261016-2154/BepInEx/plugins/000shcdese/SHCDESE.dll'
$current=Join-Path $GameDir 'BepInEx/plugins/000shcdese/SHCDESE.dll'
if((Get-ContinuationHash $prior) -cne 'E5D78FDF2EA9701336C398F7D29410F206D141FC01AA8099F30C824E6AB8A0B3' -or (Get-ContinuationHash $current) -cne '577F9AAB5DE504BE0BFA2193C7DEB734B26076ECBBAF0A408FC9ABC4CC5E7175'){throw 'Unaudited Extender comparison identity.'}
[Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $GameDir 'BepInEx/core/Mono.Cecil.dll')))|Out-Null
function Describe-Type($Type){
    @($Type.FullName;foreach($field in $Type.Fields){$field.FullName};foreach($method in $Type.Methods){
        $method.FullName;[string]$method.Attributes
        if($method.HasBody){
            $method.Body.MaxStackSize;$method.Body.InitLocals
            foreach($v in $method.Body.Variables){$v.ToString()+':'+$v.VariableType.FullName}
            foreach($i in $method.Body.Instructions){$i.ToString()}
            foreach($e in $method.Body.ExceptionHandlers){[string]$e.HandlerType+'|'+$e.TryStart+'|'+$e.TryEnd+'|'+$e.HandlerStart+'|'+$e.HandlerEnd+'|'+$e.FilterStart+'|'+$e.CatchType}
        }
    };foreach($nested in $Type.NestedTypes){Describe-Type $nested}) -join "`n"
}
$old=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($prior);$new=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($current)
try{
    foreach($name in @('SHCDESE.API.Components.Archive.MapModManager','SHCDESE.API.Components.Archive.MapArchive','SHCDESE.API.Components.ModManager.GameAssetModManager','SHCDESE.API.Components.ModManager.ModInfo','ModDependency')){
        $a=$old.MainModule.GetType($name);$b=$new.MainModule.GetType($name)
        if(-not $a -or -not $b -or (Describe-Type $a) -cne (Describe-Type $b)){throw "Changed Extender contract: $name"}
        Write-Host "PASS: complete IL/fields/locals/EH continuity: $name"
    }
}finally{$old.Dispose();$new.Dispose()}
Write-Host 'Static continuity only. New Extender runtime acceptance remains pending.'
