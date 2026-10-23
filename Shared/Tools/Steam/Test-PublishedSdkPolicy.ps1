$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'ApiSharedSdkPolicy.ps1')
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$checks=0
function Check($Ok,$Message){if(-not $Ok){throw $Message};$script:checks++}
function Reject([scriptblock]$Action){$failed=$false;try{& $Action|Out-Null}catch{$failed=$true};Check $failed 'Expected invalid SDK rejection'}
# Production lookup with inert external-command doubles: no network, build or publication.
$ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $workspace 'Shared/Tools/Release/Release.Common.ps1'),[ref]$null,[ref]$null)
$node=$ast.Find({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq 'Get-PublishedApiSharedRelease'},$true)
. ([scriptblock]::Create($node.Extent.Text))
$savedTest=$env:SHCDE_WORKSHOP_TEST;$savedSelection=$env:SHCDE_API_SHARED_MAIN_SELECTION
try {
    $env:SHCDE_API_SHARED_MAIN_SELECTION='fixture-selection'
    function Get-WorkshopApiRoot($Workspace){return 'fixture'}
    function Invoke-CheckedCommand($FilePath,$Arguments){
        if($FilePath -eq 'git'){return @{Output=@($script:commit+"`trefs/tags/"+$script:tag)}}
        Check ($Arguments[2] -ceq $script:tag) 'Lookup changed the selected SDK tag'
        return @{ExitCode=0;Output=@((@{tagName=$script:tag;isDraft=$script:draft;url='fixture';assets=@(@{name=('APIShared-v'+$script:version+'.zip')})}|ConvertTo-Json -Depth 5))}
    }
    foreach($script:version in @('0.9.2','0.12.3','1.4.0')) {
        foreach($test in @($false,$true)) {
            $env:SHCDE_WORKSHOP_TEST=if($test){'1'}else{''}
            $script:commit='a'*40;$script:draft=$test
            $script:tag=if($test){'APIShared/test/v'+$version+'-'+$commit.Substring(0,12)}else{'APIShared/v'+$version}
            $package=[pscustomobject]@{Version=$version;Commit=$commit}
            $config=@{Root=$workspace;ApiShared=@{Project='APIShared';Repository='community/api'}}
            $result=Get-PublishedApiSharedRelease $config $package
            Check ($result.Tag -ceq $tag) 'Correct artifact not retained'
            $script:draft=-not $test
            Reject {Get-PublishedApiSharedRelease $config $package}
            $script:draft=$test
            Reject {Get-PublishedApiSharedRelease $config ([pscustomobject]@{Version=$version;Commit=('b'*40)})}
            $provenance=[pscustomobject]@{ApiSharedCommit=$commit;ApiSharedMainCommit=$commit;Infrastructure=@([pscustomobject]@{
                Guid='APIShared_Serp';Version=$version;SourceCommit=$commit;ReleaseTag=$tag;PackageSha256=('c'*64)})}
            $identity=Get-ModpackApiSharedIdentity $provenance $test
            Assert-ModpackApiSharedIdentity $identity (Get-ModpackApiSharedIdentity $provenance $test);$checks++
            Reject {Get-ModpackApiSharedIdentity $provenance (-not $test)}
            $provenance.Infrastructure[0].PackageSha256='d'*64
            Reject {Assert-ModpackApiSharedIdentity $identity (Get-ModpackApiSharedIdentity $provenance $test)}
            $provenance.Infrastructure[0].PackageSha256='invalid'
            Reject {Get-ModpackApiSharedIdentity $provenance $test}
        }
    }
    $assets=@([pscustomobject]@{name='package.zip';state='uploaded';size=3;digest=('sha256:'+('a'*64))})
    $records=@(Get-ApiSharedReleaseAssetRecords $assets @('package.zip'))
    Check ($records.Count -eq 1 -and $records[0].Sha256 -ceq ('a'*64)) 'Canonical release digest was not preserved'
    Reject {Get-ApiSharedReleaseAssetRecords @($assets+$assets) @('package.zip')}
    $assets[0].digest=$null
    Reject {Get-ApiSharedReleaseAssetRecords $assets @('package.zip')}
    $assets[0].digest='sha256:'+('b'*64)
    $assets[0].state='new'
    Reject {Get-ApiSharedReleaseAssetRecords $assets @('package.zip')}
    Assert-ApiSharedMinimumVersion '0.12.3' '0.12.3';$checks++
    Assert-ApiSharedMinimumVersion '1.4.0' '0.12.3';$checks++
    Reject {Assert-ApiSharedMinimumVersion '0.9.2' '0.12.3'}
    Write-Host "PASS: $checks version-independent SDK, hash identity and mode checks."
} finally {$env:SHCDE_WORKSHOP_TEST=$savedTest;$env:SHCDE_API_SHARED_MAIN_SELECTION=$savedSelection}
