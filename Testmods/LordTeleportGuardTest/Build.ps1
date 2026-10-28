[CmdletBinding()]
param([Parameter(ValueFromRemainingArguments=$true)][string[]]$BuildArguments)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$extender = Join-Path $game 'BepInEx\plugins\000shcdese'
$sdk = Join-Path $workspace 'APIShared\BepInEx\plugins\APIShared_Serp'
$installedSdk = Join-Path $game 'BepInEx\plugins\APIShared_Serp'
$compiler = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
$native = Join-Path $game 'Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll'
$diagnostic = $BuildArguments -icontains '/devdiag'
$noInstall = $BuildArguments -icontains '/noinstall'
$env:SHCDE_DEVDIAG = if ($diagnostic) { '1' } else { $null }
function Log([string]$message) { Write-Host ('[{0:HH:mm:ss.fff}] {1}' -f [DateTime]::Now,$message) }
function Hash([string]$path) {
    $stream=[IO.File]::OpenRead($path); $sha=[Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','') }
    finally { $sha.Dispose(); $stream.Dispose() }
}
function RunCompiler([string]$project,[string]$output,[string]$intermediate,[bool]$diag,[string]$logPath) {
    $choice=if($diag){'true'}else{'false'}
    # A trailing backslash escapes the closing quote in Windows PowerShell 5.1 native arguments.
    $arguments=@($project,'/t:Rebuild','/nologo','/verbosity:minimal',('/p:GameDir='+$game),('/p:ExtenderDir='+$extender),('/p:ApiSharedDir='+$sdk),('/p:SerpDevDiagnostics='+$choice),('/p:OutputPath='+$output+'/'),('/p:BaseIntermediateOutputPath='+$intermediate+'/'))
    & $compiler @arguments 2>&1 | Tee-Object -FilePath $logPath | Out-Host
    if($LASTEXITCODE -ne 0){throw "Compilation failed: $project ($logPath)"}
}
try {
    foreach($argument in $BuildArguments){if($argument -notin @('/devdiag','/nopause','/noinstall','/source:working')){throw "Unsupported argument: $argument. This test driver builds current working sources."}}
    . (Join-Path $workspace 'Shared\Tools\Paths\WorkPaths.ps1')
    $run = New-ShortWorkDirectory -Root $workspace -Kind a -Name lord-build -Identity 'Current working sources; existing verified SDK; no dependency rebuild or installation'
    Log "Build evidence: $run"
    $others = @{}
    foreach($area in @('plugins','patchers')){
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $game ('BepInEx\'+$area)) -Filter '*.dll' -Recurse -File){
            if($file.FullName -notlike '*\LordTeleportGuardTest_Serp\*'){$others[$file.FullName]=Hash $file.FullName}
        }
    }
    Log 'Scoped source, lifecycle, dependency, CRLF and permanent-hook preflight'
    & (Join-Path $workspace 'Shared\Tools\Validation\Test-SharedBoundaries.ps1') -Workspace $workspace -ModDirectory 'Testmods\LordTeleportGuardTest' -ConsumerOnly
    if(-not $?){throw 'Scoped preflight failed.'}
    & (Join-Path $workspace 'Shared\Tools\Validation\Test-DevDiagnostics.ps1') -Root $workspace -ModDirectory 'Testmods\LordTeleportGuardTest'
    if($LASTEXITCODE -ne 0){throw 'Diagnostic inventory failed.'}
    & (Join-Path $workspace 'Shared\Tools\ApiSharedRepository\Test-ConsumerPackage.ps1') -Workspace $workspace -PackageDirectory $sdk
    if($LASTEXITCODE -ne 0){throw 'Existing SDK proof failed; dependencies were not rebuilt.'}
    if((Hash (Join-Path $sdk 'APIShared.dll')) -cne (Hash (Join-Path $installedSdk 'APIShared.dll'))){throw 'Verified SDK differs from installed APIShared.dll; no dependency is replaced by this driver.'}
    $inputFiles=@(Get-ChildItem -LiteralPath $PSScriptRoot -File -Recurse | Where-Object { $_.FullName -notmatch '[\\/](bin|obj|BepInEx)[\\/]' })
    foreach($relative in @('Shared\Runtime\Diagnostics\DebugLogHelper.cs','Shared\Runtime\Diagnostics\DevDiag.cs','Shared\Runtime\Native\NativePatternResolver.cs','Directory.Build.targets','Shared\Tools\Paths\Paths.targets','Shared\Tools\Paths\TestWorkDirectory.cs')){
        $inputFiles+=Get-Item -LiteralPath (Join-Path $workspace $relative)
    }
    $inputs=@($inputFiles | ForEach-Object { [ordered]@{Path=$_.FullName;Sha256=(Hash $_.FullName)} })
    foreach($mode in @('normal','devdiag')){
        $diag=$mode -eq 'devdiag'; $output=Join-Path $run $mode; $testOutput=Join-Path $run ($mode+'-tests')
        Log "Compile $mode artifact and its actual native execution tests"
        RunCompiler (Join-Path $PSScriptRoot 'LordTeleportGuardTest.csproj') $output (Join-Path $run ('obj-'+$mode)) $diag (Join-Path $run ($mode+'-build.log'))
        RunCompiler (Join-Path $PSScriptRoot 'tests\LordTeleportGuard.Tests.csproj') $testOutput (Join-Path $run ('obj-tests-'+$mode)) $diag (Join-Path $run ($mode+'-test-build.log'))
        & (Join-Path $testOutput 'LordTeleportGuard.Tests.exe') $native (Join-Path $output 'LordTeleportGuardTest.dll') 2>&1 | Tee-Object -FilePath (Join-Path $run ($mode+'-tests.log')) | Out-Host
        if($LASTEXITCODE -ne 0){throw "$mode native execution failed."}
    }
    foreach($inputRecord in $inputs){if((Hash $inputRecord.Path) -cne $inputRecord.Sha256){throw "Source changed during build: $($inputRecord.Path)"}}
    $selected=Join-Path $run $(if($diagnostic){'devdiag'}else{'normal'})
    $local=Join-Path $PSScriptRoot 'BepInEx\plugins\LordTeleportGuardTest_Serp'
    [void][IO.Directory]::CreateDirectory($local)
    foreach($name in @('LordTeleportGuardTest.dll','LordTeleportGuardTest.pdb')){Copy-Item -LiteralPath (Join-Path $selected $name) -Destination (Join-Path $local $name) -Force}
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'info.json') -Destination (Join-Path $local 'info.json') -Force
    $target=Join-Path $game 'BepInEx\plugins\LordTeleportGuardTest_Serp'
    $installed=@()
    if(-not $noInstall){
        if(Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue){throw 'Game is running; verified package is ready but installation was not attempted.'}
        Log 'Install only LordTeleportGuardTest_Serp'
        [void][IO.Directory]::CreateDirectory($target)
        if((Get-Item -LiteralPath $target).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Installation target is a link.'}
        foreach($file in Get-ChildItem -LiteralPath $local -File){
            $destination=Join-Path $target $file.Name
            Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
            $hash=Hash $file.FullName
            if((Hash $destination) -cne $hash){throw "Installed hash mismatch: $destination"}
            $installed+= [ordered]@{Path=$destination;Sha256=$hash}
        }
    }
    foreach($path in $others.Keys){if((Hash $path) -cne $others[$path]){throw "Unrelated DLL changed during the build: $path"}}
    $referenceProof=@($others.GetEnumerator() | Sort-Object Name | ForEach-Object {[ordered]@{Path=$_.Key;Sha256=$_.Value}})
    $record=[ordered]@{ObservedUtc=[DateTime]::UtcNow.ToString('o');Source='working';Head=(& git -C $workspace rev-parse HEAD);Diagnostic=$diagnostic;NativeSha256=(Hash $native);Inputs=$inputs;Installed=$installed;UnchangedOtherDlls=$others.Count;OtherDlls=$referenceProof;GameAcceptance='Pending';Evidence=$run}
    [IO.File]::WriteAllText((Join-Path $run 'result.json'),(($record | ConvertTo-Json -Depth 8)+"`r`n"),[Text.UTF8Encoding]::new($false))
    Log "PASS: normal and diagnostic execution; $($others.Count) other DLL hashes unchanged; installation=$(-not $noInstall); evidence=$run"
    exit 0
} catch {
    Log ('ERROR: '+$_.Exception.Message)
    if($run){[IO.File]::WriteAllText((Join-Path $run 'failure.txt'),($_.ToString()+"`r`n"))}
    exit 1
}
