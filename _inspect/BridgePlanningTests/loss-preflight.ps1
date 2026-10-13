[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$workspace=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$mod=Join-Path $workspace 'Testmods/EnemyBridgePathTest'
$installed=[Reflection.Assembly]::LoadFrom('E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins\000shcdese\SHCDESE.dll')
$event=$installed.GetType('SHCDESE.EventAPI.Units.UnitMoveHereEventArgs',$true)
foreach($name in @('UnitId','TileX','TileY','Unknown')) {
    $property=$event.GetProperty($name)
    if(!$property -or !$property.GetMethod.IsPublic -or $property.PropertyType -ne [int]){throw "Installed unit event signature changed: $name"}
}
$source=[IO.File]::ReadAllText((Join-Path $mod 'src/BridgeLossHistory.cs'))
if($source -notmatch '0x5f011a0' -or $source -match '\b(?:Marshal\.Write|FindNext|SetPath|TrySet|Dispose)\b'){throw 'Passive moat/task-index history contract changed'}
$trace=[IO.File]::ReadAllText((Join-Path $mod 'src/BridgeStructureTrace.cs'))
if($trace -match 'player\s*[!=]=\s*[1-8]\b|building\s*==\s*(494|613|703)\b'){throw 'Save-specific runtime identity filter'}
if($trace -notmatch 'lossHistory.KnowsGate' -or $trace -notmatch 'ongoing-unit-state-snapshot'){throw 'Historical gate/movement evidence missing'}
$capture=[IO.File]::ReadAllText((Join-Path $mod 'src/BridgeStructureCapture.cs'))
if($capture -notmatch '0xa00' -or $capture -notmatch 'freshDecisionProof=False' -or $capture -notmatch 'SnapshotOnly'){throw 'Own-unit temporal/layout separation missing'}
if($capture -notmatch 'Copy\("profiles90",0x322540,90\*4\)' -or $capture -notmatch 'Copy\("permissions540",0x32BDB0,90\*6\*4\)' -or $capture -notmatch 'CheckOwnTables\(input\);pending=input' -or $capture -notmatch 'CheckOwnTables\(input\);input.Sections.Add\("post/players9"'){throw 'Own writable command table capacity or stability contract changed'}
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $mod 'verify.ps1')
if($LASTEXITCODE -ne 0){throw 'Runtime verification failed'}
Write-Host 'PASS installed event members, raw native slot-window layout, dynamic selection, historical damage and passive lifecycle gates'
if($trace -notmatch 'ValidateStructurePair' -or $trace -notmatch 'kind==3\|\|kind==4\?x:tx'){throw 'Own unit goal must retain event coordinates and root-end identity validation'}
$shadow=[IO.File]::ReadAllText((Join-Path $mod 'src/BridgeVirtualShadow.cs'))
if($shadow -notmatch 'EnqueuePair' -or $shadow -notmatch 'awaiting-own-military-root-completion' -or $shadow -notmatch 'planningCommandPairComplete'){throw 'Paired bounded publication contract missing'}
if($capture -notmatch 'Copy\("lineMode",0x2f7a690,4\)' -or $capture -notmatch 'Copy\("lineBuildingClasses336",0x2e68d0,336\*4\)' -or $capture -notmatch 'Copy\("lineBlockGrid",0x51d75f0,320800\)'){throw 'Hash-audited own line-input capacities changed'}
Write-Host 'PASS own event goals, current identities, pair publication and bounded line capture'

$countMethod=$installed.GetType('SHCDESE.API.GamePathingManagerAPI',$true).GetMethod('GetNativeComponentTileCounts')
if(!$countMethod -or !$countMethod.IsPublic -or $countMethod.GetParameters().Length -ne 0 -or $countMethod.ReturnType.ToString() -ne 'System.Span`1[System.Int32]'){throw 'Installed effective count view mismatch'}
$planning=[IO.File]::ReadAllText((Join-Path $mod 'src/BridgePlanningCapture.cs'))
if($planning -notmatch 'effectiveCountsPre' -or $planning -notmatch 'effective-count-table-changed-during-selection' -or $planning -notmatch 'values.Length!=1000&&values.Length!=4000'){throw 'Own bounded effective count input capture missing'}
if($capture -notmatch 'pathInputCoverage4' -or $capture -match 'if\(BitConverter.ToInt32\(input.Sections\["pre/lineMode"\],0\)!=0\)Copy\("lineBlockGrid"'){throw 'Normal flood fallback grid is not captured independently of line mode'}
Write-Host 'PASS installed public effective count view, input capacities and normal-command flood grid'

$decision=[IO.File]::ReadAllText((Join-Path $mod 'src/BridgeDecisionTrace.cs'))
if($decision -notmatch 'effectiveCounts:\(\)=>GamePathingManagerAPI.Instance.GetNativeComponentTileCounts\(\).ToArray\(\)'){throw 'Relocated counts must use installed public live view at selector Pre'}
Write-Host 'PASS existing observer invokes public count provider without a new hook or live write'
