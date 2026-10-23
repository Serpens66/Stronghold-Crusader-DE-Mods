[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
. (Join-Path $workspace 'Shared/Tools/Paths/WorkPaths.ps1')
. (Join-Path $workspace 'Shared/Tools/ScriptExtenderUpdate/ScriptExtenderUpdate.Common.ps1')
$fixture = New-ShortWorkDirectory -Root $workspace -Kind t -Name runtime-inputs -Identity 'Evaluated runtime Compile/Reference selection'
function Write-Fixture([string]$Relative, [string]$Text) {
    $path = Join-Path $fixture $Relative
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path))
    [IO.File]::WriteAllText($path, ($Text -replace '\r?\n', "`r`n"), [Text.UTF8Encoding]::new($false))
}
$mod = [pscustomobject]@{ Name='RuntimeInputFixture'; Plugin='runtime/Plugin.cs'; Project='mod/Runtime.csproj' }
$project = @'
<Project>
  <Import Project="sources.props" />
  <ItemGroup>
    <Compile Include="runtime/**/*.cs" Exclude="runtime/excluded/**/*.cs" />
    <Compile Remove="runtime/removed.cs" />
    <Compile Include="../linked/*.cs" />
    <Compile Include="conditional.cs" Condition="'$(Configuration)' == 'Debug'" />
  </ItemGroup>
</Project>
'@
Write-Fixture 'mod/Runtime.csproj' $project
Write-Fixture 'mod/sources.props' '<Project />'
Write-Fixture 'mod/runtime/Plugin.cs' 'class RuntimePlugin : BaseUnityPlugin { void Awake() { } }'
Write-Fixture 'linked/Safe.cs' 'class LinkedRuntime { }'
$unsafeJson = 'class UnsafeRuntime { object Read(string text) { return System.Text.Json.JsonSerializer.Deserialize<object>(text); } }'
Write-Fixture 'mod/tools/Program.cs' $unsafeJson
Write-Fixture 'mod/tests/Case.cs' $unsafeJson
Write-Fixture 'mod/runtime/excluded/Bad.cs' $unsafeJson
Write-Fixture 'mod/runtime/removed.cs' $unsafeJson
Write-Fixture 'mod/conditional.cs' $unsafeJson
$checks = 0
function Accept([string]$Reason) {
    Assert-SERuntimeModPreflight $mod $fixture
    $script:checks++
    Write-Host "PASS: $Reason"
}
function Reject([string]$Reason, [string]$Expected) {
    $rejected = $false
    try { Assert-SERuntimeModPreflight $mod $fixture }
    catch { if ($_.Exception.Message -notmatch $Expected) { throw }; $rejected = $true }
    if (-not $rejected) { throw "Unsafe runtime accepted: $Reason" }
    $script:checks++
    Write-Host "PASS: rejected $Reason"
}
Accept 'neighboring CLI/tests, exclusions, removals and inactive conditions are outside runtime compilation'
Write-Fixture 'linked/Safe.cs' $unsafeJson
Reject 'wildcard-linked source outside project directory' 'forbidden runtime JSON'
Write-Fixture 'linked/Safe.cs' 'class LinkedRuntime { }'
Write-Fixture 'mod/runtime/Added.cs' $unsafeJson
Reject 'new wildcard runtime source' 'forbidden runtime JSON'
Write-Fixture 'mod/runtime/Added.cs' 'class AddedRuntime { }'
Write-Fixture 'mod/sources.props' '<Project><ItemGroup><Reference Include="System.Text.Json" /></ItemGroup></Project>'
Reject 'imported forbidden runtime reference' 'Reference: System.Text.Json'
Write-Fixture 'mod/sources.props' '<Project><ItemGroup><Compile Include="tools/Program.cs" /></ItemGroup></Project>'
Reject 'explicit runtime source regardless of its directory name' 'forbidden runtime JSON'
Write-Fixture 'mod/sources.props' '<Project><ItemGroup><Compile Include="missing.cs" /></ItemGroup></Project>'
Reject 'missing evaluated source' 'missing runtime source'
Write-Fixture 'mod/sources.props' '<Project><ItemGroup><Compile Include="conditional.cs" Condition="''$(Configuration)'' == ''Release''" /></ItemGroup></Project>'
Reject 'active conditional runtime source' 'forbidden runtime JSON'
Write-Fixture 'mod/Runtime.csproj' '<Project />'
Reject 'empty evaluated runtime source set' 'no Compile inputs'
Write-Fixture 'mod/Runtime.csproj' $project
Write-Fixture 'mod/sources.props' '<Project />'
Write-Fixture 'linked/Safe.cs' 'class LinkedRuntime { void OnDestroy() { subscription.Dispose(); } }'
Reject 'linked startup teardown' 'startup lifecycle method reaches teardown'
Write-Fixture 'linked/Safe.cs' 'class LinkedRuntime { }'
Accept 'corrected evaluated project passes again'
Write-Host "PASS: $checks evaluated runtime input regressions; fixtures retained at $fixture"
