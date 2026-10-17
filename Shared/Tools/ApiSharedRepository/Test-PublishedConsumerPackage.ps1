$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ApiShared.Common.ps1')
. (Join-Path $PSScriptRoot '../Paths/WorkPaths.ps1')
$fixture = New-ShortWorkDirectory -Root ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))) -Kind t -Name 'PublishedConsumerPackage'
$previousProof = $env:SHCDE_API_SHARED_RELEASE_PROOF
$previousTest = $env:SHCDE_WORKSHOP_TEST
$previousDirectory = $env:SHCDE_API_SHARED_DIR
function Write-SdkFixture([string]$Path, [string]$Text) {
    [void][IO.Directory]::CreateDirectory((Split-Path -Parent $Path))
    [IO.File]::WriteAllText($Path, $Text, [Text.UTF8Encoding]::new($false))
}
function Expect-SdkFailure([scriptblock]$Action, [string]$Message) {
    $failed = $false
    try { & $Action | Out-Null } catch { $failed = $_.Exception.Message -match $Message }
    if (-not $failed) { throw "Expected SDK rejection: $Message" }
}
try {
    $api = Join-Path $fixture 'APIShared'
    [void][IO.Directory]::CreateDirectory($api)
    Invoke-ApiGit $api @('init','--initial-branch=main') | Out-Null
    Write-SdkFixture (Join-Path $api 'info.json') '{"GUID":"APIShared_Serp","Version":"0.7.1"}'
    Write-SdkFixture (Join-Path $api 'src/Runtime.cs') 'original runtime'
    Invoke-ApiGit $api @('add','.') | Out-Null
    Invoke-ApiGit $api @('-c','user.name=Fixture','-c','user.email=fixture@example.invalid','commit','-m','release') | Out-Null
    $commit = ((Invoke-ApiGit $api @('rev-parse','HEAD')) -join '').Trim()
    Write-SdkFixture (Join-Path $api 'tests/Internal.ps1') 'test-only change'
    Invoke-ApiGit $api @('add','.') | Out-Null
    Invoke-ApiGit $api @('-c','user.name=Fixture','-c','user.email=fixture@example.invalid','commit','-m','tests') | Out-Null
    $cache = Join-Path $fixture '.release-output/APIShared/cache/v0.7.1'
    $package = Join-Path $cache 'audit/BepInEx/plugins/APIShared_Serp'
    $dll = Join-Path $package 'APIShared.dll'
    Write-SdkFixture $dll 'published binary'
    $zip = Join-Path $cache 'APIShared-v0.7.1.zip'
    Write-SdkFixture $zip 'downloaded archive fixture'
    $proofPath = Join-Path $cache 'APIShared-v0.7.1.provenance.json'
    $proof = @{ SchemaVersion=1; Mod='APIShared'; PluginGuid='APIShared_Serp'; Version='0.7.1'; Tag='APIShared/v0.7.1'; Commit=$commit
        Package=@{ Sha256=(Get-ApiRepositoryHash $zip); Files=@(@{ Path='BepInEx/plugins/APIShared_Serp/APIShared.dll'; Size=(Get-Item $dll).Length; Sha256=(Get-ApiRepositoryHash $dll) }) } }
    Write-SdkFixture $proofPath ($proof | ConvertTo-Json -Depth 8)
    $env:SHCDE_API_SHARED_RELEASE_PROOF = $proofPath
    $env:SHCDE_WORKSHOP_TEST = ''
    $result = Assert-ApiConsumerPackage $fixture $package
    if ($result.Commit -cne $commit) { throw 'Published source provenance was replaced with the current commit.' }
    . (Join-Path $PSScriptRoot '../Release/Release.Common.ps1')
    # Import the real workspace targets into an isolated workspace and exercise
    # MSBuild property precedence plus its fresh PowerShell validation subprocess.
    $workspaceRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
    Copy-Item -LiteralPath (Join-Path $workspaceRoot 'Directory.Build.targets') -Destination (Join-Path $fixture 'Directory.Build.targets')
    foreach ($relative in @('Shared/Tools/Paths/WorkPaths.ps1','Shared/Tools/Paths/Paths.targets','Shared/Tools/Paths/Configure-Executable.ps1','Shared/Tools/Paths/TestWorkDirectory.cs','Shared/Tools/ApiSharedRepository/ApiShared.Common.ps1','Shared/Tools/ApiSharedRepository/MainSource.Common.ps1','Shared/Tools/ApiSharedRepository/RepositoryProcess.Common.ps1','Shared/Tools/ApiSharedRepository/Test-ConsumerPackage.ps1','Shared/Tools/Steam/SteamPackPolicy.ps1')) {
        $destination = Join-Path $fixture $relative
        [void][IO.Directory]::CreateDirectory((Split-Path -Parent $destination))
        Copy-Item -LiteralPath (Join-Path $workspaceRoot $relative) -Destination $destination
    }
    $localPackage = Join-Path $api 'BepInEx/plugins/APIShared_Serp'
    Write-SdkFixture (Join-Path $localPackage 'APIShared.dll') 'local binary'
    Write-SdkFixture (Join-Path $api 'tools/BuildProof.ps1') 'function Assert-ApiBuildProof { param([string]$Root,[switch]$Release) if ([IO.File]::ReadAllText((Join-Path $Root "BepInEx/plugins/APIShared_Serp/APIShared.dll")) -cne "local binary") { throw "Invalid fixture local package" }; return [pscustomobject]@{ Commit="local-fixture" } }'
    $probe = Join-Path $fixture 'Consumer/nested/Probe.proj'
    Write-SdkFixture $probe @'
<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <ItemGroup><Reference Include="APIShared"><HintPath>obsolete-reference.dll</HintPath></Reference></ItemGroup>
  <Import Project="../../Directory.Build.targets" />
  <Target Name="Probe" DependsOnTargets="ValidateWorkspaceApiSharedPackage">
    <Error Condition="'$(ApiSharedDir)' != '$(ExpectedSdk)'" Text="Wrong SDK property: $(ApiSharedDir)" />
    <Error Condition="'%(Reference.HintPath)' != '$(ExpectedSdk)\APIShared.dll'" Text="Compiler and validation SDK differ: %(Reference.HintPath)" />
    <Message Importance="high" Text="SDK_PROBE_OK $(ApiSharedDir)" />
  </Target>
</Project>
'@
    $parent = Join-Path $fixture 'Parent.proj'
    Write-SdkFixture $parent '<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003"><Target Name="Probe"><MSBuild Projects="Consumer/nested/Probe.proj" Targets="Probe" /></Target></Project>'
    $configuredMsBuild = (Get-ReleaseConfiguration).MSBuild
    function Invoke-SdkMSBuild([string]$ExpectedSdk, [string]$ExplicitSdk, [bool]$ShouldPass) {
        $arguments = @($parent,'/nologo','/t:Probe','/v:minimal',"/p:ExpectedSdk=$ExpectedSdk")
        if ($ExplicitSdk) { $arguments += "/p:ApiSharedDir=$ExplicitSdk" }
        $output = if (Test-Path -LiteralPath $configuredMsBuild -PathType Leaf) { @(& $configuredMsBuild @arguments 2>&1) } else { @(& dotnet msbuild @arguments 2>&1) }
        $code = $LASTEXITCODE
        if ($ShouldPass) {
            if ($code -ne 0 -or ($output -join "`n") -notmatch 'SDK_PROBE_OK') { throw "SDK MSBuild regression failed: $($output -join '`n')" }
        } elseif ($code -eq 0 -or ($output -join "`n") -notmatch 'SDK path does not match') { throw "MSBuild did not reject a mismatched SDK proof: $($output -join '`n')" }
    }
    $env:SHCDE_API_SHARED_RELEASE_PROOF = ''
    $env:SHCDE_API_SHARED_DIR = ''
    Invoke-SdkMSBuild $localPackage '' $true
    $env:SHCDE_API_SHARED_RELEASE_PROOF = $proofPath
    $env:SHCDE_API_SHARED_DIR = $package
    Invoke-SdkMSBuild $package '' $true
    # An explicit global property must win even over a conflicting environment value.
    $env:SHCDE_API_SHARED_DIR = $localPackage
    Invoke-SdkMSBuild $package $package $true
    $env:SHCDE_API_SHARED_DIR = $package
    Invoke-SdkMSBuild $localPackage $localPackage $false
    Write-Host 'PASS: real nested MSBuild SDK selection, explicit/environment/default precedence, matching compiler references and strict release proof.'
    $env:SHCDE_API_SHARED_DIR = $package
    $consumer = Join-Path $fixture 'Consumer'
    Write-SdkFixture (Join-Path $consumer 'Consumer.csproj') '<Project><ItemGroup><Reference Include="APIShared"><HintPath>$(ApiSharedDir)/APIShared.dll</HintPath></Reference></ItemGroup></Project>'
    $game = Join-Path $fixture 'Game'
    Write-SdkFixture (Join-Path $game 'Stronghold Crusader Definitive Edition_Data/Plugins/x86_64/CrusaderDE.dll') 'native fixture'
    $metadata = [pscustomobject]@{ ModDir=$consumer; Config=[pscustomobject]@{ Root=$fixture; GameDir=$game } }
    $dependencies = @(Get-DependencyRecords -Metadata $metadata -ExtenderDir (Join-Path $fixture 'Extender'))
    $apiRecord = @($dependencies | Where-Object Name -eq 'APIShared.dll')
    if ($apiRecord.Count -ne 1 -or $apiRecord[0].Sha256 -cne $proof.Package.Files[0].Sha256 -or $apiRecord[0].Path -notmatch '/\.release-output/APIShared/') { throw 'Consumer provenance must record the selected published SDK.' }
    Write-SdkFixture $dll 'changed binary'
    Expect-SdkFailure { Assert-ApiConsumerPackage $fixture $package } 'hash mismatch'
    Write-SdkFixture $dll 'published binary'
    Write-SdkFixture (Join-Path $package 'Extra.dll') 'undeclared'
    Expect-SdkFailure { Assert-ApiConsumerPackage $fixture $package } 'Undeclared'
    Remove-Item -LiteralPath (Join-Path $package 'Extra.dll')
    Remove-Item -LiteralPath $dll
    Expect-SdkFailure { Assert-ApiConsumerPackage $fixture $package } 'hash mismatch'
    Write-SdkFixture $dll 'published binary'
    Write-SdkFixture (Join-Path $api 'src/Runtime.cs') 'changed runtime'
    Expect-SdkFailure { Assert-ApiConsumerPackage $fixture $package } 'differs from current package sources'
    Write-SdkFixture (Join-Path $api 'src/Runtime.cs') 'original runtime'
    Write-SdkFixture (Join-Path $api 'src/New.cs') 'untracked runtime'
    Expect-SdkFailure { Assert-ApiConsumerPackage $fixture $package } 'differs from current package sources'
    Remove-Item -LiteralPath (Join-Path $api 'src/New.cs')
    Expect-SdkFailure { Assert-ApiConsumerPackage $fixture (Join-Path $api 'BepInEx/plugins/APIShared_Serp') } 'SDK path'
    $env:SHCDE_WORKSHOP_TEST = '1'
    Expect-SdkFailure { Assert-ApiConsumerPackage $fixture $package } 'selected mode'
    $env:SHCDE_WORKSHOP_TEST = ''
    $link = Join-Path $cache 'linked-audit'
    [void](New-Item -ItemType Junction -Path $link -Target (Join-Path $cache 'audit'))
    # A junction replacing the package directory must not bypass file ownership checks.
    $originalPackage = $package + '-original'
    Move-Item -LiteralPath $package -Destination $originalPackage
    [void](New-Item -ItemType Junction -Path $package -Target $originalPackage)
    Expect-SdkFailure { Assert-ApiConsumerPackage $fixture $package } 'contains a link'
    [IO.Directory]::Delete($package)
    Move-Item -LiteralPath $originalPackage -Destination $package
    [IO.Directory]::Delete($link)
    $testCache = Join-Path $fixture '.release-output/SteamWorkshop/test/SerpsMods/cache/APIShared/v0.7.1-fixture'
    $testPackage = Join-Path $testCache 'audit/BepInEx/plugins/APIShared_Serp'
    Write-SdkFixture (Join-Path $testPackage 'APIShared.dll') 'published binary'
    Write-SdkFixture (Join-Path $testCache 'APIShared-v0.7.1.zip') 'downloaded archive fixture'
    $proof.Tag = 'APIShared/test/v0.7.1-' + $commit.Substring(0,12)
    $testProof = Join-Path $testCache 'APIShared-v0.7.1.provenance.json'
    Write-SdkFixture $testProof ($proof | ConvertTo-Json -Depth 8)
    $env:SHCDE_API_SHARED_RELEASE_PROOF = $testProof
    $env:SHCDE_WORKSHOP_TEST = '1'
    $null = Assert-ApiConsumerPackage $fixture $testPackage
    $env:SHCDE_WORKSHOP_TEST = ''
    Expect-SdkFailure { Assert-ApiConsumerPackage $fixture $testPackage } 'selected mode'
    $env:SHCDE_API_SHARED_RELEASE_PROOF = $proofPath
    $proof.Tag = 'APIShared/v0.7.1'
    $proof.Package.Files += $proof.Package.Files[0]
    Write-SdkFixture $proofPath ($proof | ConvertTo-Json -Depth 8)
    Expect-SdkFailure { Assert-ApiConsumerPackage $fixture $package } 'duplicate'
    $proof.Package.Files = @($proof.Package.Files[0])
    Write-SdkFixture $proofPath ($proof | ConvertTo-Json -Depth 8)
    Write-SdkFixture $zip 'corrupt archive'
    Expect-SdkFailure { Assert-ApiConsumerPackage $fixture $package } 'ZIP hash'
    Write-Host 'PASS: published SDK reuse, retained provenance, runtime changes, mode/path conflicts and complete file hashes.'
} finally {
    $env:SHCDE_API_SHARED_RELEASE_PROOF = $previousProof
    $env:SHCDE_WORKSHOP_TEST = $previousTest
    $env:SHCDE_API_SHARED_DIR = $previousDirectory
    if ([IO.Path]::GetFullPath($fixture).StartsWith([IO.Path]::GetTempPath(), [StringComparison]::OrdinalIgnoreCase)) { Remove-Item -LiteralPath $fixture -Recurse -Force -ErrorAction SilentlyContinue }
}
