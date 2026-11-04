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
$validator = Join-Path $PSScriptRoot 'Test-HostPackage.ps1'
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
function Get-InstalledSnapshot {
    # Include every sibling and protected file, plus directories (including empty ones).
    $entries = @(foreach ($entry in Get-ChildItem -LiteralPath $plugins -Recurse -Force | Sort-Object FullName) {
        $relative = $entry.FullName.Substring($plugins.Length + 1)
        if ($entry.PSIsContainer) { 'D:' + $relative }
        else { 'F:' + $relative + ':' + [Convert]::ToBase64String([IO.File]::ReadAllBytes($entry.FullName)) }
    })
    return ($entries -join "`n")
}
function Write-Identity([string]$Version = '2.3.4') {
    Write-Fixture (Join-Path $package 'info.json') ('{"GUID":"SerpsMods_Serp","Version":"' + $Version + '"}')
    Write-Fixture (Join-Path $package 'serps-modpack.json') ('{"SchemaVersion":2,"PackGuid":"SerpsMods_Serp","HostVersion":"' + $Version + '","PackVersion":"' + $Version + '","Infrastructure":[],"Mods":[]}')
}
# Real managed DLLs exercise production metadata reading without loading game code.
$assemblies = @{}
foreach ($version in @('2.3.4.0','2.3.5.0','2.3.4.7')) {
    $dll = Join-Path $testRoot ('dll-' + $version + '/SerpsModsHost.dll')
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($dll))
    $source = '[assembly: System.Reflection.AssemblyVersion("' + $version + '")] public class HostFixture_' + $version.Replace('.','_') + ' {}'
    Add-Type -TypeDefinition $source -OutputAssembly $dll -OutputType Library
    $assemblies[$version] = $dll
}
foreach ($name in @('Override/Example.xaml','Locales/en.txt')) {
    Write-Fixture (Join-Path $package $name) ('new ' + $name)
}
Write-Identity
[IO.File]::Copy($assemblies['2.3.4.0'], (Join-Path $package 'SerpsModsHost.dll'))
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
$installedSnapshot = Get-InstalledSnapshot

# The independent package gate is also the build's /noinstall gate. Every invalid
# package must fail there AND in the real installer before any existing file changes.
$badPackages = @(
    @{ Name='DLL version'; File='SerpsModsHost.dll'; Assembly='2.3.5.0' },
    @{ Name='nonzero DLL revision'; File='SerpsModsHost.dll'; Assembly='2.3.4.7' },
    @{ Name='invalid DLL'; File='SerpsModsHost.dll'; Text='not an assembly' },
    @{ Name='info version'; File='info.json'; Text='{"GUID":"SerpsMods_Serp","Version":"2.3.5"}' },
    @{ Name='invalid version'; File='info.json'; Text='{"GUID":"SerpsMods_Serp","Version":"invalid"}' },
    @{ Name='info GUID'; File='info.json'; Text='{"GUID":"Other","Version":"2.3.4"}' },
    @{ Name='host version'; File='serps-modpack.json'; Field='HostVersion'; Value='2.3.5' },
    @{ Name='pack version'; File='serps-modpack.json'; Field='PackVersion'; Value='2.3.5' },
    @{ Name='pack GUID'; File='serps-modpack.json'; Field='PackGuid'; Value='Other' },
    @{ Name='schema'; File='serps-modpack.json'; Field='SchemaVersion'; Value=1 },
    @{ Name='invalid info JSON'; File='info.json'; Text='{' },
    @{ Name='invalid pack JSON'; File='serps-modpack.json'; Text='{' },
    @{ Name='non-object info'; File='info.json'; Text='[]' },
    @{ Name='non-object pack'; File='serps-modpack.json'; Text='null' },
    @{ Name='missing info'; File='info.json' },
    @{ Name='missing pack'; File='serps-modpack.json' },
    @{ Name='missing DLL'; File='SerpsModsHost.dll' }
)
foreach ($case in $badPackages) {
    Write-Identity
    [IO.File]::Copy($assemblies['2.3.4.0'], (Join-Path $package 'SerpsModsHost.dll'), $true)
    $path = Join-Path $package $case.File
    if ($case.ContainsKey('Assembly')) { [IO.File]::Copy($assemblies[$case.Assembly], $path, $true) }
    elseif ($case.ContainsKey('Text')) { Write-Fixture $path $case.Text }
    elseif ($case.ContainsKey('Field')) {
        $data = [IO.File]::ReadAllText($path) | ConvertFrom-Json
        $data.($case.Field) = $case.Value
        Write-Fixture $path ($data | ConvertTo-Json)
    }
    else { Remove-Item -LiteralPath $path }
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $validator -PackageDirectory $package | Out-Host
    Assert-True ($LASTEXITCODE -ne 0) ("Invalid package gate accepted: " + $case.Name)
    Assert-True ((Invoke-Installer $package $target) -ne 0) ("Invalid installer package accepted: " + $case.Name)
    Assert-True ((Get-InstalledSnapshot) -ceq $installedSnapshot) ("Rejected install changed files: " + $case.Name)
}
Write-Identity '2.3.4.7'
[IO.File]::Copy($assemblies['2.3.4.7'], (Join-Path $package 'SerpsModsHost.dll'), $true)
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $validator -PackageDirectory $package | Out-Host
Assert-True ($LASTEXITCODE -eq 0) 'Matching four-component version was rejected.'
Write-Identity
[IO.File]::Copy($assemblies['2.3.4.0'], (Join-Path $package 'SerpsModsHost.dll'), $true)
foreach ($badTarget in @($plugins,(Join-Path $plugins 'Other'),(Join-Path $testRoot 'outside/SerpsMods_Serp_dev'))) {
    Assert-True ((Invoke-Installer $package $badTarget) -ne 0) 'Unsafe destination was accepted.'
}
Assert-True ((Invoke-Installer $target $target) -ne 0) 'Overlapping source/target was accepted.'
Write-Fixture (Join-Path $package 'Infrastructure/APIShared_Serp/APIShared.dll') 'overwrite'
Assert-True ((Invoke-Installer $package $target) -ne 0) 'Package containing protected infrastructure was accepted.'
Assert-True (([IO.File]::ReadAllText((Join-Path $target 'Infrastructure/APIShared_Serp/APIShared.dll'))) -ceq 'independent') 'Rejected protected-data overwrite modified infrastructure.'
Assert-True ((Get-InstalledSnapshot) -ceq $installedSnapshot) 'Rejected path/protected-data install changed existing output.'
Write-Host "PASS: host installation, version/package validation, preservation, repeated install and fail-closed paths ($testRoot)."
