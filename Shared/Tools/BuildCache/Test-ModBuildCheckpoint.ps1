$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'ModBuildCheckpoint.ps1')
. (Join-Path $PSScriptRoot '../Paths/WorkPaths.ps1')
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$fixture=New-ShortWorkDirectory -Root $workspace -Kind t -Name ModBuildCheckpoint
$checks=0;$script:runs=0;$script:exitCode=0
function Check([bool]$Value,[string]$Message){if(-not $Value){throw $Message};$script:checks++}
function Put([string]$Path,[string]$Value){[void][IO.Directory]::CreateDirectory((Split-Path -Parent $Path));[IO.File]::WriteAllText($Path,$Value,[Text.UTF8Encoding]::new($false))}
$mod=Join-Path $fixture 'Mod';$package=Join-Path $mod 'BepInEx/plugins/Mod_Serp'
$game=Join-Path $fixture 'game';$extender=Join-Path $fixture 'extender';$api=Join-Path $fixture 'api';$sdk=Join-Path $api 'BepInEx/plugins/APIShared_Serp'
foreach($directory in @('Stronghold Crusader Definitive Edition_Data/Managed','Stronghold Crusader Definitive Edition_Data/Plugins/x86_64','BepInEx/core')){[void][IO.Directory]::CreateDirectory((Join-Path $game $directory))}
Put (Join-Path $game 'Stronghold Crusader Definitive Edition_Data/Managed/Assembly-CSharp.dll') 'managed'
Put (Join-Path $game 'Stronghold Crusader Definitive Edition_Data/Plugins/x86_64/CrusaderDE.dll') 'native'
Put (Join-Path $extender 'SHCDESE.dll') 'extender'
Put (Join-Path $sdk 'APIShared.dll') 'sdk'
Put (Join-Path $api 'src/Contract.cs') 'public contract'
Put (Join-Path $api 'info.json') '{}'
Put (Join-Path $mod 'src/Settings.cs') 'class Settings {}'
Put (Join-Path $mod 'tests/Test.cs') 'class Test {}'
Put (Join-Path $mod 'Mod.csproj') '<Project><PropertyGroup><XamlPatcherSource>$(ScriptExtenderSourceRoot)\src\XAML</XamlPatcherSource></PropertyGroup><ItemGroup><Compile Include="src/Settings.cs" /><Compile Include="$(XamlPatcherSource)\XamlPatcher.cs" /></ItemGroup></Project>'
Put (Join-Path $fixture 'shcde-script-extender/src/XAML/XamlPatcher.cs') 'extender source'
Put (Join-Path $mod 'build.bat') '@echo fixture'
Put (Join-Path $fixture 'compiler/MSBuild.exe') 'compiler'
Put (Join-Path $fixture 'Shared/Runtime/Contract.cs') 'shared'
$options=@{Workspace=$fixture;Coordinator=$fixture;ModName='Mod';ModDirectory=$mod;PackageDirectory=$package;
    Driver=(Join-Path $mod 'build.bat');Arguments=@('/nopause','/noinstall');Commit=('a'*40);GameDirectory=$game;
    ExtenderDirectory=$extender;ApiSharedDirectory=$sdk;ApiSourceDirectory=$api;Compiler=(Join-Path $fixture 'compiler/MSBuild.exe')}
$stage=Join-Path $fixture '.release-output/Mod/v1/stage/Mod'
$options.RetainedPackageDirectory=$stage
$cache=Join-Path $fixture '.release-output/checkpoints/mod-builds';$record=Join-Path $cache 'Mod.json'
function Invoke-Fixture {
    Invoke-ModBuildCheckpoint @options -Action {
        $script:runs++
        if($script:exitCode -eq 0){Put (Join-Path $package 'Mod.dll') 'validated output';Put (Join-Path $package 'info.json') '{}'}
        [pscustomobject]@{ExitCode=$script:exitCode;Output=@('fixture build')}
    }
}
$oldBaseline=$env:SHCDE_NATIVE_BASELINE_ROOT;$oldEnvironment=$env:MOD_CHECKPOINT_TEST;$oldExtender=$env:SHCDESE_EXTENDER_DIR
try {
    $baseline=Join-Path $fixture 'baseline';$env:SHCDE_NATIVE_BASELINE_ROOT=$baseline
    Put (Join-Path $baseline 'CURRENT.json') '{"semanticDirectory":"sem/current","databaseManifest":"sem/current/DATABASE_INFO.json"}'
    Put (Join-Path $baseline 'sem/current/DATABASE_INFO.json') '{"database":{"path":"sem/current/analysis/index.sqlite","referenceSha256":"fixture digest"}}'
    Put (Join-Path $baseline 'sem/current/analysis/index.sqlite') 'database fixture'
    Put (Join-Path $baseline 'sem/current/contracts.csv') 'contract'
    Put (Join-Path $baseline 'sem/current/resources/xaml/Assets/GUI/XAML/IngameUIScreens.xaml') '<Grid />'
    $env:SHCDESE_EXTENDER_DIR=Join-Path $fixture 'explicit-extender'
    Put (Join-Path $env:SHCDESE_EXTENDER_DIR 'SHCDESE.dll') 'effective override'
    $first=Invoke-Fixture
    Check ($script:runs -eq 1 -and -not $first.Checkpoint.Reused) 'First success did not produce a checkpoint.'
    $second=Invoke-Fixture
    Check ($script:runs -eq 1 -and $second.Checkpoint.Reused) 'Unchanged completed build was rerun.'
    Check ($second.Checkpoint.BuildCommit -ceq ('a'*40) -and $second.Checkpoint.BuildStartedUtc -ceq $first.Checkpoint.BuildStartedUtc -and
        $second.Checkpoint.BuildCompletedUtc -ceq $first.Checkpoint.BuildCompletedUtc) 'Reuse rewrote original build identity/timestamps.'
    $before=$script:runs;$savedArguments=$options.Arguments;$options.Arguments=@('/nopause')
    $null=Invoke-Fixture;$null=Invoke-Fixture
    Check ($script:runs -eq $before+2) 'Installing build action was skipped by package-only reuse.'
    $options.Arguments=$savedArguments
    [void][IO.Directory]::CreateDirectory($stage)
    foreach($file in Get-ChildItem -LiteralPath $package -File){Copy-Item -LiteralPath $file.FullName -Destination $stage}
    Put (Join-Path $package 'Mod.dll') 'restored tracked package'
    $before=$script:runs;$retained=Invoke-Fixture
    Check ($script:runs -eq $before -and $retained.Checkpoint.Reused -and $retained.PackageDirectory -ieq $stage) 'Restored source package did not reuse the intact release stage.'
    Check ($retained.Checkpoint.BuildCommit -ceq $first.Checkpoint.BuildCommit -and $retained.Checkpoint.BuildCompletedUtc -ceq $first.Checkpoint.BuildCompletedUtc -and
        -not $retained.Checkpoint.PSObject.Properties['PackageDirectory']) 'Retained reuse changed original identity or exposed its local path in provenance.'
    foreach($mutation in @('modify','add','remove')) {
        Put (Join-Path $stage 'Mod.dll') 'validated output';Put (Join-Path $stage 'info.json') '{}'
        $extra=Join-Path $stage 'extra.txt';if([IO.File]::Exists($extra)){[IO.File]::Delete($extra)}
        if($mutation -eq 'modify'){Put (Join-Path $stage 'Mod.dll') 'tampered stage'}
        elseif($mutation -eq 'add'){Put $extra 'extra'}
        else{[IO.File]::Delete((Join-Path $stage 'info.json'))}
        Put (Join-Path $package 'Mod.dll') 'restored tracked package'
        $before=$script:runs;$result=Invoke-Fixture
        Check ($script:runs -eq $before+1 -and -not $result.Checkpoint.Reused) ('Invalid retained stage '+$mutation+' skipped the build.')
    }
    $savedStage=$options.RetainedPackageDirectory;$options.RetainedPackageDirectory=Join-Path $fixture 'outside-release-output'
    $rejected=$false;try{$null=Invoke-Fixture}catch{$rejected=$_.Exception.Message -like '*escapes release output*'}
    Check $rejected 'Retained package outside the controlled release output was accepted.'
    $options.RetainedPackageDirectory=$savedStage
    foreach($file in @((Join-Path $mod 'src/Settings.cs'),(Join-Path $mod 'tests/Test.cs'),(Join-Path $fixture 'Shared/Runtime/Contract.cs'),
        (Join-Path $sdk 'APIShared.dll'),(Join-Path $api 'src/Contract.cs'),(Join-Path $extender 'SHCDESE.dll'),
        (Join-Path $game 'Stronghold Crusader Definitive Edition_Data/Plugins/x86_64/CrusaderDE.dll'),
        (Join-Path $game 'Stronghold Crusader Definitive Edition_Data/Managed/Assembly-CSharp.dll'),$options.Compiler,
        (Join-Path $baseline 'sem/current/contracts.csv'),(Join-Path $baseline 'sem/current/resources/xaml/Assets/GUI/XAML/IngameUIScreens.xaml'),
        (Join-Path $fixture 'shcde-script-extender/src/XAML/XamlPatcher.cs'),(Join-Path $env:SHCDESE_EXTENDER_DIR 'SHCDESE.dll'))) {
        $before=$script:runs;[IO.File]::AppendAllText($file,' changed');$result=Invoke-Fixture
        Check ($script:runs -eq $before+1 -and -not $result.Checkpoint.Reused) ('Input mutation reused stale success: '+$file)
    }
    $before=$script:runs;$options.Arguments=@('/nopause','/noinstall','/tests:quick');$result=Invoke-Fixture
    Check ($script:runs -eq $before+1) 'Argument change did not invalidate.'
    $before=$script:runs;$options.Commit='b'*40;$result=Invoke-Fixture
    Check ($script:runs -eq $before+1) 'Commit change did not invalidate.'
    $before=$script:runs;$env:MOD_CHECKPOINT_TEST='different';$result=Invoke-Fixture
    Check ($script:runs -eq $before+1) 'Inherited environment change did not invalidate.'
    foreach($mutation in @('modify','add','remove')) {
        $before=$script:runs
        if($mutation -eq 'modify'){Put (Join-Path $package 'Mod.dll') 'tampered'}
        elseif($mutation -eq 'add'){Put (Join-Path $package 'extra.txt') 'extra'}
        else{[IO.File]::Delete((Join-Path $package 'extra.txt'))}
        $result=Invoke-Fixture
        Check ($script:runs -eq $before+1) ('Package '+$mutation+' did not invalidate.')
    }
    $script:exitCode=27;$options.Arguments+= '/force-failure';$failure=Invoke-Fixture
    Check ($failure.ExitCode -eq 27 -and -not [IO.File]::Exists($record)) 'A failed build retained a success checkpoint.'
    $script:exitCode=0;$before=$script:runs;$result=Invoke-Fixture
    Check ($script:runs -eq $before+1 -and -not $result.Checkpoint.Reused) 'Failed execution was cached.'
    # A key-computation failure must also retire the former success before executing.
    $compilerBytes=[IO.File]::ReadAllBytes($options.Compiler);[IO.File]::Delete($options.Compiler);$script:exitCode=13
    $failure=Invoke-Fixture
    Check ($failure.ExitCode -eq 13 -and -not [IO.File]::Exists($record)) 'Unavailable fingerprint resurrected an older success.'
    [IO.File]::WriteAllBytes($options.Compiler,$compilerBytes);$script:exitCode=0;$before=$script:runs;$result=Invoke-Fixture
    Check ($script:runs -eq $before+1) 'Key recovery reused a checkpoint retired by failure.'
    [IO.File]::WriteAllText($record,'broken JSON');$before=$script:runs;$result=Invoke-Fixture
    Check ($script:runs -eq $before+1) 'Malformed metadata was trusted.'
    for($index=0;$index -lt 35;$index++){Put (Join-Path $cache ('old'+$index+'.json')) '{}'}
    [IO.File]::AppendAllText((Join-Path $mod 'src/Settings.cs'),' bounded');$result=Invoke-Fixture
    Check (@(Get-ChildItem -LiteralPath $cache -File -Filter '*.json').Count -le 32) 'Checkpoint record count is unbounded.'
    Check (@(Get-ChildItem -LiteralPath $cache -File|Where-Object Extension -notin @('.json','.lock')).Count -eq 0) 'Cache retained binary/output copies.'
    $lock=[IO.File]::Open((Join-Path $cache 'Mod.lock'),[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    try{$rejected=$false;try{$null=Invoke-Fixture}catch [IO.IOException]{$rejected=$true};Check $rejected 'Concurrent package reuse/build was not excluded.'}
    finally{$lock.Dispose()}
    Write-Host "PASS: $checks completed-build checkpoint reuse, invalidation, original identity, failure, metadata bounds and concurrency checks. Fixture: $fixture"
} finally {$env:SHCDE_NATIVE_BASELINE_ROOT=$oldBaseline;$env:MOD_CHECKPOINT_TEST=$oldEnvironment;$env:SHCDESE_EXTENDER_DIR=$oldExtender}
