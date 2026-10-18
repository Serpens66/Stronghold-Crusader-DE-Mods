[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../../Shared/Tools/Paths/WorkPaths.ps1')
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$testRoot = New-ShortWorkDirectory -Root $workspace -Kind t -Name host-install
$package = Join-Path $testRoot 'package'
$plugins = Join-Path $testRoot 'game/BepInEx/plugins'
$target = Join-Path $plugins 'SerpsMods_Serp_dev'
$installer = Join-Path $PSScriptRoot 'Install-HostPackage.ps1'
function Assert-True([bool]$Value,[string]$Message) { if (-not $Value) { throw $Message } }
function Write-Fixture([string]$Path,[string]$Value) {
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path))
    [IO.File]::WriteAllText($Path,$Value)
}
function Invoke-Installer([string]$Source,[string]$Output) {
    # Match build.bat's fresh Windows PowerShell process, never a copied implementation.
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $installer -PackageDirectory $Source -PluginRoot $plugins -Destination $Output | Out-Host
    return $LASTEXITCODE
}
foreach ($name in @('SerpsModsHost.dll','info.json','serps-modpack.json','Override/Example.xaml','Locales/en.txt')) {
    Write-Fixture (Join-Path $package $name) ('new ' + $name)
}
Write-Fixture (Join-Path $target 'Infrastructure/APIShared_Serp/APIShared.dll') 'independent'
Write-Fixture (Join-Path $target 'LobbyModSettings/settings.json') 'settings'
Write-Fixture (Join-Path $target 'old/sub/file.txt') 'obsolete'
Write-Fixture (Join-Path $target 'old.dll') 'obsolete'
$main = Join-Path $plugins 'SerpsMods_Serp/sentinel.txt'
$separateApi = Join-Path $plugins 'APIShared_Serp_dev/APIShared.dll'
Write-Fixture $main 'main'
Write-Fixture $separateApi 'separate'
Assert-True ((Invoke-Installer $package $target) -eq 0) 'Valid dev installation failed.'
Assert-True (([IO.File]::ReadAllText((Join-Path $target 'LobbyModSettings/settings.json'))) -ceq 'settings') 'Player settings changed.'
Assert-True (([IO.File]::ReadAllText((Join-Path $target 'Infrastructure/APIShared_Serp/APIShared.dll'))) -ceq 'independent') 'Infrastructure changed.'
Assert-True (([IO.File]::ReadAllText($main)) -ceq 'main' -and ([IO.File]::ReadAllText($separateApi)) -ceq 'separate') 'Sibling installation changed.'
Assert-True (-not (Test-Path -LiteralPath (Join-Path $target 'old')) -and -not (Test-Path -LiteralPath (Join-Path $target 'old.dll'))) 'Obsolete output survived.'
foreach ($file in Get-ChildItem -LiteralPath $package -Recurse -File) {
    $relative = $file.FullName.Substring($package.Length + 1)
    Assert-True (([Convert]::ToBase64String([IO.File]::ReadAllBytes($file.FullName))) -ceq ([Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $target $relative))))) "Copied content differs: $relative"
}
Assert-True ((Invoke-Installer $package $target) -eq 0) 'Repeated installation failed.'
foreach ($badTarget in @($plugins,(Join-Path $plugins 'Other'),(Join-Path $testRoot 'outside/SerpsMods_Serp_dev'))) {
    Assert-True ((Invoke-Installer $package $badTarget) -ne 0) 'Unsafe destination was accepted.'
}
Assert-True ((Invoke-Installer $target $target) -ne 0) 'Overlapping source/target was accepted.'
Remove-Item -LiteralPath (Join-Path $package 'SerpsModsHost.dll')
Assert-True ((Invoke-Installer $package $target) -ne 0) 'Incomplete package was accepted.'
Assert-True (([IO.File]::ReadAllText((Join-Path $target 'SerpsModsHost.dll'))) -ceq 'new SerpsModsHost.dll') 'Rejected install modified existing output.'
Write-Fixture (Join-Path $package 'SerpsModsHost.dll') 'new SerpsModsHost.dll'
Write-Fixture (Join-Path $package 'Infrastructure/APIShared_Serp/APIShared.dll') 'overwrite'
Assert-True ((Invoke-Installer $package $target) -ne 0) 'Package containing protected infrastructure was accepted.'
Assert-True (([IO.File]::ReadAllText((Join-Path $target 'Infrastructure/APIShared_Serp/APIShared.dll'))) -ceq 'independent') 'Rejected protected-data overwrite modified infrastructure.'
Write-Host "PASS: host installation, preservation, repeated install and fail-closed paths ($testRoot)."
