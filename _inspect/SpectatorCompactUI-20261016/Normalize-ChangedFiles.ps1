$ErrorActionPreference = 'Stop'
$mod = Join-Path (Get-Location) 'Testmods\SpectatorEditorBuildTest'
$relative = @('src\CreditAmountInput.cs','src\TestActionPolicy.cs','src\TestToolsViewModel.cs','src\TestToolsRuntime.cs','src\SpectatorEditorBuildTestPlugin.cs','src\SpectatorEditorBuildTestPlugin.Hud.cs','tests\ActionPolicyTests.cs','tests\Test-InstalledContracts.ps1','tests\Test-CompactLayout.ps1','verify.ps1','Patches\Assets\GUI\XAML\IngameUIScreens.xaml')
$paths = @($relative | ForEach-Object { Join-Path $mod $_ }) + @(Get-ChildItem -LiteralPath (Join-Path $mod 'Locales') -Filter '*.txt' -File | ForEach-Object FullName)
foreach ($path in $paths) {
    $before = [IO.File]::ReadAllText($path)
    $expected = [regex]::Replace($before, '\r?\n', [string][char]13 + [char]10)
    [IO.File]::WriteAllText($path, $expected, [Text.UTF8Encoding]::new($false))
    $after = [IO.File]::ReadAllText($path)
    if (-not [string]::Equals($after, $expected, [StringComparison]::Ordinal) -or [regex]::IsMatch($after, '(?<!\r)\n')) { throw "CRLF verification failed: $path" }
    Write-Host ((Split-Path -Leaf $path) + ': CRLF=' + [regex]::Matches($after, '\r\n').Count + ', bareLF=0')
}
