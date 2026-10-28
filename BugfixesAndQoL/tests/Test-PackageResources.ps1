[CmdletBinding()]
param([Parameter(Mandatory)][string]$ApiSharedDir, [Parameter(Mandatory)][string]$MSBuild)
$ErrorActionPreference = 'Stop'
$modRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$workspace = Split-Path -Parent $modRoot
. (Join-Path $workspace 'Shared/Tools/Paths/WorkPaths.ps1')
. (Join-Path $workspace 'Shared/Tools/Release/Release.Common.ps1')
$fixture = New-ShortWorkDirectory -Root $workspace -Kind t -Name 'BfqResources'
$package = Join-Path $fixture 'pkg'
$verifier = Join-Path $modRoot 'Test-PackageResources.ps1'
$buildNumber = 0
$verificationNumber = 0
function Build-Fixture {
    $script:buildNumber++
    $log = Join-Path $fixture ('build-' + $script:buildNumber + '.log')
    & $MSBuild (Join-Path $modRoot 'BugfixesAndQoL.csproj') /t:Build /p:Configuration=Release "/p:ApiSharedDir=$ApiSharedDir" "/p:OutputPath=$package/" "/p:IntermediateOutputPath=$fixture/obj/" /verbosity:minimal *> $log
    if ($LASTEXITCODE -ne 0) { Get-Content -LiteralPath $log -Tail 20; throw "Resource fixture build failed: $log" }
}
function Verify-Fixture([bool]$ShouldPass, [string]$Target = $package, [string]$Reference = '', [switch]$InstalledCopy) {
    $script:verificationNumber++
    $arguments = @('-NoProfile','-ExecutionPolicy','Bypass','-File',$verifier,'-SourceDirectory',$modRoot,'-PackageDirectory',$Target)
    if ($Reference) { $arguments += @('-ReferencePackage',$Reference) }
    if ($InstalledCopy) { $arguments += '-Installed' }
    $previousPreference = $ErrorActionPreference
    try {
        # Windows PowerShell turns native stderr into NativeCommandError records. Expected
        # rejection must be observed through the child exit code rather than aborting this test.
        $ErrorActionPreference = 'Continue'
        & powershell.exe @arguments *> (Join-Path $fixture ('verification-' + $script:verificationNumber + '.log'))
        $verificationExit = $LASTEXITCODE
    } finally { $ErrorActionPreference = $previousPreference }
    if (($verificationExit -eq 0) -ne $ShouldPass) { throw "Unexpected resource verification result; expected pass=$ShouldPass; fixture=$fixture" }
}
Build-Fixture
Verify-Fixture $true
# This shipped path must remain a valid neutral patch: omitting it would leave older
# Extender installations with their active replacement and removed ViewModel bindings.
$escRelative = 'Patches/Assets/GUI/XAMLResources/HUD_IngameMenu.xaml'
[xml]$esc = [IO.File]::ReadAllText((Join-Path $package $escRelative))
if ($esc.DocumentElement.LocalName -cne 'Patch' -or @($esc.DocumentElement.ChildNodes | Where-Object NodeType -eq Element).Count) {
    throw 'The retired ESC replacement is not a neutral Patch document.'
}
$future = [DateTime]::UtcNow.AddDays(2)
foreach ($folder in @('Patches','Override','Locales')) {
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $package $folder) -Recurse -File) {
        [IO.File]::WriteAllText($file.FullName, 'stale package bytes', [Text.UTF8Encoding]::new($false))
        [IO.File]::SetLastWriteTimeUtc($file.FullName, $future)
    }
}
[IO.File]::WriteAllText((Join-Path $package 'info.json'), '{}', [Text.UTF8Encoding]::new($false))
[IO.File]::SetLastWriteTimeUtc((Join-Path $package 'info.json'), $future)
Verify-Fixture $false
Build-Fixture # Incremental build must still overwrite every future-dated resource.
Verify-Fixture $true
$escHash = Get-Sha256Hex -Path (Join-Path $package $escRelative)
if ($escHash -cne (Get-Sha256Hex -Path (Join-Path $modRoot $escRelative))) { throw 'Neutral ESC patch was not restored.' }
$removed = Join-Path $package $escRelative
[IO.File]::Delete($removed)
Verify-Fixture $false
Build-Fixture
Verify-Fixture $true
$installed = Join-Path $fixture 'installed'
Copy-Item -LiteralPath $package -Destination $installed -Recurse
$userSettings = Join-Path $installed 'LobbyModSettings/player.msgpack'
[void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($userSettings))
[IO.File]::WriteAllText($userSettings, 'player settings', [Text.UTF8Encoding]::new($false))
$userLocale = Join-Path $installed 'Locales/private.txt'
[IO.File]::WriteAllText($userLocale, 'private locale', [Text.UTF8Encoding]::new($false))
Verify-Fixture $true $installed $package -InstalledCopy
if ([IO.File]::ReadAllText($userSettings) -cne 'player settings' -or [IO.File]::ReadAllText($userLocale) -cne 'private locale') {
    throw 'Installed-file validation changed user data.'
}
[IO.File]::WriteAllText((Join-Path $installed 'BugfixesAndQoL.dll'), 'changed assembly', [Text.UTF8Encoding]::new($false))
Verify-Fixture $false $installed $package -InstalledCopy
$unexpected = Join-Path $package 'Patches/unclaimed.xaml'
[IO.File]::WriteAllText($unexpected, '<Patch/>', [Text.UTF8Encoding]::new($false))
Verify-Fixture $false
if (-not [IO.File]::Exists($unexpected)) { throw 'Validation must not remove unclaimed files.' }
Write-Host 'PASS: actual incremental project builds replace future-dated stale resources, retain the neutral ESC patch, and restore missing files; validation rejects stale/missing/unclaimed resources without deletion.'
Write-Host "Fixture evidence: $fixture"
