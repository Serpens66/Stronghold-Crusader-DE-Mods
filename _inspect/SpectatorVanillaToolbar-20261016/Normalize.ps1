$ErrorActionPreference = 'Stop'
$root = Join-Path (Get-Location).Path 'Testmods/SpectatorEditorBuildTest'
$relative = @('src/CreditAmountInput.cs','src/VanillaPowerToolbar.cs','src/TestToolsPowers.cs','src/TestToolsViewModel.cs','src/TestToolsRuntime.cs','src/TestToolPolicies.cs','src/SpectatorEditorBuildTestPlugin.Hud.cs','tests/ActionPolicyTests.cs','tests/CreditFocusTests.cs','tests/CreditFocusTests.csproj','tests/Test-CreditFocus.ps1','tests/Test-CompactLayout.ps1','tests/Test-InstalledContracts.ps1','verify.ps1','UpdateToNewDLL.md','Patches/Assets/GUI/XAML/IngameUIScreens.xaml')
$paths = @($relative | ForEach-Object { Join-Path $root $_ }) + @(Get-ChildItem -LiteralPath (Join-Path $root 'Locales') -Filter '*.txt' | ForEach-Object FullName)
foreach ($path in $paths) {
    $expected = [IO.File]::ReadAllText($path).Replace("`r`n", "`n").Replace("`n", "`r`n")
    [IO.File]::WriteAllText($path, $expected, [Text.UTF8Encoding]::new($false))
    if ([IO.File]::ReadAllText($path) -cne $expected -or $expected -match '(?<!\r)\n|\r(?!\n)') { throw "CRLF/readback failed: $path" }
}
Write-Host "Verified CRLF and exact readback for $($paths.Count) changed files."
