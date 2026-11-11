[CmdletBinding()]
param([string]$GameDir=$env:SHCDE_GAME_DIR,[string]$ApiSharedDir=$env:SHCDE_API_SHARED_DIR,[string]$MsBuild=$env:SHCDE_MSBUILD,[switch]$DevDiag,[switch]$NoInstall)
$ErrorActionPreference='Stop'
$project=Split-Path -Parent $PSScriptRoot
$workspace=[IO.Path]::GetFullPath((Join-Path $project '..\..'))
. (Join-Path $workspace 'Shared\Tools\Paths\WorkPaths.ps1')
. (Join-Path $workspace 'APIShared\tools\BuildProof.ps1')
if(!$GameDir){$GameDir='E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'}
$GameDir=(Resolve-Path -LiteralPath $GameDir).Path
if(!$ApiSharedDir){$ApiSharedDir=Join-Path $GameDir 'BepInEx\plugins\APIShared_Serp'}
$ApiSharedDir=(Resolve-Path -LiteralPath $ApiSharedDir).Path
$extender=Join-Path $GameDir 'BepInEx\plugins\000shcdese'
if(!$MsBuild){$MsBuild='C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'}
$MsBuild=(Resolve-Path -LiteralPath $MsBuild).Path
$head=& git -C $workspace rev-parse HEAD
if($LASTEXITCODE -ne 0){throw 'Cannot identify source checkout.'}
if($DevDiag){$branch=& git -C $workspace branch --show-current;if($LASTEXITCODE -ne 0 -or $branch -eq 'main'){throw 'Diagnostic builds require a non-main checkout.'}}
$manifestPath=Join-Path $project 'info.json'
$manifest=Get-Content -LiteralPath $manifestPath -Raw|ConvertFrom-Json
$localeDirectory=Join-Path $project 'Locales'
$englishKeys=@(Get-Content -LiteralPath (Join-Path $localeDirectory 'en-US.txt') | Where-Object {$_ -match '^[^#=]+='} | ForEach-Object {$_.Substring(0,$_.IndexOf('='))} | Sort-Object)
foreach($localeFile in Get-ChildItem -LiteralPath $localeDirectory -File -Filter '*.txt'){
 $localeKeys=@(Get-Content -LiteralPath $localeFile.FullName | Where-Object {$_ -match '^[^#=]+='} | ForEach-Object {$_.Substring(0,$_.IndexOf('='))} | Sort-Object)
 if((Compare-Object $englishKeys $localeKeys -CaseSensitive) -or @($localeKeys|Group-Object|Where-Object Count -gt 1).Count){throw "Locale key mismatch: $($localeFile.Name)"}
}
$sdkInputs=@((Join-Path $ApiSharedDir 'APIShared.dll'),(Join-Path $GameDir 'BepInEx\patchers\APIShared.DependencyBootstrap.dll'),(Join-Path $extender 'SHCDESE.dll'),(Join-Path $GameDir 'Stronghold Crusader Definitive Edition_Data\Managed\Assembly-CSharp.dll'),(Join-Path $GameDir 'Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll'))
$sdkHashes=@{};foreach($path in $sdkInputs){$sdkHashes[$path]=Get-ApiFileHash $path}
$sourceInputs=@(Get-ChildItem -LiteralPath $project -Recurse -File | Where-Object {
 $_.FullName.Substring($project.Length) -notmatch '[\\/](bin|obj|\.local)[\\/]' -and $_.Extension -in @('.cs','.csproj','.ps1','.bat','.json','.md','.txt')
} | ForEach-Object FullName) + @((Join-Path $workspace 'Shared\Runtime\Diagnostics\DevDiag.cs'))
$sourceHashes=@{};foreach($path in $sourceInputs){$sourceHashes[$path]=Get-ApiFileHash $path}
foreach($dependency in @(@{Path=$ApiSharedDir;Guid='APIShared_Serp';Min=$manifest.Dependencies[0].MinimumVersion},@{Path=$extender;Guid='000shcdese';Min=$manifest.MinimumScriptExtenderVersion})){
 $found=Get-Content -LiteralPath (Join-Path $dependency.Path 'info.json') -Raw|ConvertFrom-Json
 if($found.GUID -ne $dependency.Guid -or [version]$found.Version -lt [version]$dependency.Min){throw "Incompatible selected dependency: $($dependency.Guid)."}
}
$run=New-ShortWorkDirectory -Root $workspace -Kind a -Name 'area-build' -Identity "AreaDemolitionTest working sources at $head"
$package=Join-Path $run 'package'
$output=Join-Path $package ('BepInEx\plugins\'+$manifest.GUID)
[void][IO.Directory]::CreateDirectory($output)
Write-Host "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss.fff')] AreaDemolitionTest build: $run"
$validation=Join-Path $workspace 'Shared\Tools\Validation'
& (Join-Path $validation 'Test-SharedBoundaries.ps1') -Workspace $workspace -ModDirectory 'Testmods\AreaDemolitionTest' -ConsumerOnly
if(!$?){throw 'Scoped source boundary validation failed.'}
$testProjects=@(Get-ChildItem -LiteralPath (Join-Path $project 'tests') -Recurse -File -Filter '*.csproj'|Sort-Object FullName)
foreach($testProject in $testProjects){
 $testLog=Join-Path $run ($testProject.Directory.Name+'-tests.log')
 Write-Host "[$(Get-Date -Format 'HH:mm:ss.fff')] Tests: $($testProject.Name)"
 & dotnet run --project $testProject.FullName --configuration Release *> $testLog
 if($LASTEXITCODE -ne 0){Get-Content -LiteralPath $testLog -Tail 40;throw "Tests failed: $testLog"}
 Get-Content -LiteralPath $testLog -Tail 3
}
$compileLog=Join-Path $run 'compile.log'
$diag=if($DevDiag){'true'}else{'false'}
& $MsBuild (Join-Path $project 'AreaDemolitionTest.csproj') /t:Build /nologo /v:minimal "/p:GameDir=$GameDir" "/p:ExtenderDir=$extender" "/p:ApiSharedDir=$ApiSharedDir" "/p:SerpDevDiagnostics=$diag" "/p:OutputPath=$output/" "/p:IntermediateOutputPath=$(Join-Path $run 'obj')/" *> $compileLog
if($LASTEXITCODE -ne 0){Get-Content -LiteralPath $compileLog -Tail 70;throw "Compilation failed: $compileLog"}
$dll=Join-Path $output 'AreaDemolitionTest.dll'
if([Reflection.AssemblyName]::GetAssemblyName($dll).Version.ToString(3) -ne $manifest.Version){throw 'Built version differs from manifest.'}
$unexpected=@(Get-ChildItem -LiteralPath $output -Recurse -File|Where-Object {$_.Extension -eq '.dll' -and $_.Name -ne 'AreaDemolitionTest.dll'})
if($unexpected.Count){throw 'Dependencies were copied into the consumer package.'}
$installed=$false
if(!$NoInstall){
 if(@(Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue).Count){throw 'Close the game before installing; the built package is retained.'}
 $destination=[IO.Path]::GetFullPath((Join-Path $GameDir ('BepInEx\plugins\'+$manifest.GUID)))
 $expectedParent=[IO.Path]::GetFullPath((Join-Path $GameDir 'BepInEx\plugins')).TrimEnd('\')+'\'
 if(!$destination.StartsWith($expectedParent,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($destination) -cne $manifest.GUID){throw 'Invalid installation target.'}
 [void][IO.Directory]::CreateDirectory($destination)
 Get-ChildItem -LiteralPath $output -Force|Copy-Item -Destination $destination -Recurse -Force
 foreach($item in Get-ChildItem -LiteralPath $output -Recurse -File){$relative=$item.FullName.Substring($output.Length+1);if((Get-ApiFileHash $item.FullName) -ne (Get-ApiFileHash (Join-Path $destination $relative))){throw "Installation mismatch: $relative"}}
 $installed=$true
}
foreach($path in $sdkInputs){if((Get-ApiFileHash $path) -ne $sdkHashes[$path]){throw "Installed dependency changed: $path"}}
foreach($path in $sourceInputs){if((Get-ApiFileHash $path) -ne $sourceHashes[$path]){throw "Source changed during build: $path"}}
$files=@(Get-ChildItem -LiteralPath $output -Recurse -File|ForEach-Object {@{Path=$_.FullName.Substring($output.Length+1);Sha256=Get-ApiFileHash $_.FullName}})
$zip=Join-Path $run 'AreaDemolitionTest.zip'
Compress-Archive -LiteralPath (Join-Path $package 'BepInEx') -DestinationPath $zip
$record=[ordered]@{CreatedUtc=[DateTime]::UtcNow.ToString('o');Head=$head;Source=$project;Package=$output;Archive=$zip;ArchiveSha256=Get-ApiFileHash $zip;Installed=$installed;DevDiag=$DevDiag.IsPresent;SdkHashes=$sdkHashes;SourceHashes=$sourceHashes;Files=$files;GameAcceptance='This build implies no game acceptance; consult separate automation evidence. Multiplayer acceptance remains deferred.'}
$recordText=($record|ConvertTo-Json -Depth 6)+"`r`n"
[IO.File]::WriteAllText((Join-Path $run 'result.json'),$recordText,[Text.UTF8Encoding]::new($false))
[void][IO.Directory]::CreateDirectory((Join-Path $project '.local'))
[IO.File]::WriteAllText((Join-Path $project '.local\latest-build.json'),$recordText,[Text.UTF8Encoding]::new($false))
Write-Host "PASS: $output Installed=$installed DevDiag=$DevDiag"
