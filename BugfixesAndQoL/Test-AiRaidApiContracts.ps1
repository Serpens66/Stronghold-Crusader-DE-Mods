$ErrorActionPreference='Stop'
$game='E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
# Read the installed bytes without changing download-zone metadata on the game installation.
[Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $game 'BepInEx\core\Mono.Cecil.dll'))) | Out-Null
$assembly=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $game 'BepInEx\plugins\000shcdese\SHCDESE.dll'))
$contracts=@{
 'EventHookBase'=@('get_SkipOriginalFunction');
 'GameTribeManagerAPI'=@('get_Instance','IsValidId','TryGetTribeById','GetUnits','TryGetAITribeStorageRole','TryResolveAITribeStorageRole','AttackBuildingEx');
 'GameBuildingManagerAPI'=@('get_Instance','TryGetBuildingById','IsValidId');
 'GameUnitManagerAPI'=@('get_Instance','TryGetUnitById','IsValidId');
 'GamePlayerManagerAPI'=@('get_Instance','TryGetPlayerResourcesById','IsAIPlayer');
 'GamePathingManagerAPI'=@('get_Instance','GetPathfindingContextView');
 'GameTileManagerAPI'=@('get_Instance','GetStructureLayer','GetTileVectorFromId','GetTileId','GetTileBuildingId');
 'GameTimeManagerAPI'=@('get_Instance','add_OnTick','remove_OnTick')
}
$rows=@(foreach($name in $contracts.Keys) {
 $type=@($assembly.MainModule.Types | Where-Object Name -eq $name)
 foreach($method in $contracts[$name]) {
  $members=@($type.Methods | Where-Object { $_.Name -eq $method -and $_.IsPublic })
  if($members.Count -eq 0) { throw "Nonpublic/missing API: $name.$method" }
  foreach($member in $members) { $member.FullName }
 }
})
$source=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src\AiRaidRetargetFixRuntime.cs'))
$fields=@([regex]::Matches($source,'->(r_[A-Za-z0-9_]+)') | ForEach-Object {$_.Groups[1].Value} | Sort-Object -Unique)
foreach($name in $fields) {
 $members=@($assembly.MainModule.Types | Where-Object Name -in 'GameTribe','GameBuilding','GameUnit' | ForEach-Object {$_.Fields} | Where-Object { $_.Name -eq $name -and $_.IsPublic })
 if($members.Count -eq 0) { throw "Nonpublic/missing interop field $name" }
 foreach($member in $members) { $rows += $member.FullName }
}
$rows += 'No newly introduced direct Assembly-CSharp field/property/method access; installed real game DLL remains the compilation reference.'
$workspace=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$report=Join-Path $workspace '.inspect\RaidRetargetEvaluation\MainmodInstalledApiContracts.txt'
$content=($rows -join [Environment]::NewLine)+[Environment]::NewLine
[IO.File]::WriteAllText($report,$content,[Text.UTF8Encoding]::new($false))
if([IO.File]::ReadAllText($report)-cne$content){throw 'API report write mismatch'}
$assembly.Dispose()
Write-Host "PASS: installed public API signatures and $($fields.Count) interop fields; no new private game accesses."
