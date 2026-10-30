param([switch]$PrepareOnly)
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
. (Join-Path $workspace 'Shared/Tools/Paths/WorkPaths.ps1')
$run=New-ShortWorkDirectory -Root $workspace -Kind t -Name lord-columns -Identity 'Production OverviewSettings against the existing APIShared preset harness'
$fixture=Join-Path $workspace '_inspect/HostClientPresetTests'
[xml]$project=[IO.File]::ReadAllText((Join-Path $fixture 'HostClientPresetTests.csproj'))
$ns=$project.DocumentElement.NamespaceURI
foreach($item in $project.SelectNodes('//*[@Include]')){
 $include=$item.GetAttribute('Include')
 if($item.LocalName -in @('Compile','Content','EmbeddedResource') -and $include -notmatch '\$\('){$item.SetAttribute('Include',(Join-Path $fixture $include))}
}
$properties=$project.CreateElement('PropertyGroup',$ns)
foreach($pair in @(@('StartupObject','LordOverviewPresetTests'),@('AssemblyName','LordOverviewPresetTests'),@('OutputPath',(Join-Path $run 'bin/')),@('IntermediateOutputPath',(Join-Path $run 'obj/')))) {
 $node=$project.CreateElement($pair[0],$ns);$node.InnerText=$pair[1];[void]$properties.AppendChild($node)
}
$compilerImport=$project.DocumentElement.SelectSingleNode("./*[local-name()='Import'][contains(@Project,'Microsoft.CSharp.targets')]")
[void]$project.DocumentElement.InsertBefore($properties,$compilerImport)
$items=$project.CreateElement('ItemGroup',$ns)
foreach($file in @('src/OverviewState.cs','src/OverviewLayout.cs','src/OverviewSettings.cs','tests/PresetProgram.cs')){
 $item=$project.CreateElement('Compile',$ns);$item.SetAttribute('Include',[IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../$file")));[void]$items.AppendChild($item)
}
$item=$project.CreateElement('Compile',$ns);$item.SetAttribute('Include',(Join-Path $workspace 'APIShared/src/Lobby/LobbyStateContracts.cs'));[void]$items.AppendChild($item)
[void]$project.DocumentElement.AppendChild($items)
$path=Join-Path $run 'Presets.csproj';$project.Save($path)
& 'C:/Program Files (x86)/Microsoft Visual Studio/2022/BuildTools/MSBuild/Current/Bin/MSBuild.exe' $path /t:Build /p:Configuration=Release /v:minimal
if($LASTEXITCODE){throw 'LordOverview preset harness compilation failed.'}
$exe=Join-Path $run 'bin/LordOverviewPresetTests.exe'
if($PrepareOnly){Write-Host "PRESET_TEST_EXE=$exe";Write-Host "PRESET_TEST_ROOT=$run";exit 0}
& $exe $run
if($LASTEXITCODE){throw 'LordOverview preset checks failed.'}
