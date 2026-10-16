$ErrorActionPreference = 'Stop'
$root = Join-Path (Get-Location) 'Testmods\SpectatorEditorBuildTest'
$files = @(
    'src\TestToolPolicies.cs','src\OptionalExtremePowers.cs','src\TestToolsNativeActions.cs','src\TestToolsPowers.cs','src\TestToolsGates.cs',
    'src\TestToolsRuntime.cs','src\TestToolsViewModel.cs','src\TestActionPolicy.cs','src\SpectatorEditorBuildTestPlugin.cs',
    'src\SpectatorEditorBuildTestPlugin.Native.cs','src\SpectatorEditorBuildTestPlugin.Placement.cs','src\SpectatorEditorBuildTestPlugin.Hud.cs',
    'tests\Test-ActionPolicy.ps1','tests\ActionPolicyTests.cs','tests\Test-CompactLayout.ps1','tests\Test-InstalledContracts.ps1',
    'tests\Test-NativeActions.ps1','tests\Test-OptionalPowers.ps1','tests\OptionalPowerFixture.cs','tests\OptionalPowerTests.cs',
    'tests\OptionalPowerFixture.csproj','tests\OptionalPowerTests.csproj',
    'verify.ps1','UpdateToNewDLL.md','Patches\Assets\GUI\XAML\IngameUIScreens.xaml'
)
$paths = @($files | ForEach-Object { Join-Path $root $_ }) + @(Get-ChildItem -LiteralPath (Join-Path $root 'Locales') -File | ForEach-Object FullName)
foreach ($path in $paths) {
    $expected = [regex]::Replace([IO.File]::ReadAllText($path), '\r?\n', [string][char]13 + [char]10)
    [IO.File]::WriteAllText($path, $expected, [Text.UTF8Encoding]::new($false))
    $actual = [IO.File]::ReadAllText($path)
    if (-not [string]::Equals($expected,$actual,[StringComparison]::Ordinal) -or [regex]::IsMatch($actual,'(?<!\r)\n')) { throw "CRLF mismatch: $path" }
    Write-Host ((Split-Path -Leaf $path) + ': verified CRLF=' + [regex]::Matches($actual,'\r\n').Count + ', bareLF=0')
}
