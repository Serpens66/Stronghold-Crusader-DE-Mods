# Creates a test item from the built payload package in a new .work/a/workshopload-item/<n> folder:
#   source\     prepared folder for APIShared's UploadMod.bat (private T2 test item)
#   simulated\  same files in published layout (Payload\ + apishared-workshop.json) for local T1/T4/T5 runs
# Variant 2 changes one file, removes one and adds one, so a Steam update shows how Steam
# treats changed, removed and foreign files.
param([ValidateSet(1, 2)][int]$Variant = 1)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
. (Join-Path (Get-ProbeWorkspace) 'Shared\Tools\Paths\WorkPaths.ps1')
$package = Join-Path $PSScriptRoot ('..\payload\package\' + $ProbePayloadGuid)
if (-not (Test-Path -LiteralPath (Join-Path $package 'WorkshopLoadProbe.Payload.dll'))) { throw 'Build the probe first (build.bat /noinstall).' }
$root = New-ShortWorkDirectory -Root (Get-ProbeWorkspace) -Kind a -Name 'workshopload-item' -Identity "WorkshopLoadProbe test item variant $Variant"

$plugin = Join-Path $root ('source\BepInEx\plugins\' + $ProbePayloadGuid)
[void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($plugin))
Copy-Item -LiteralPath $package -Destination $plugin -Recurse
Write-ProbeText (Join-Path $plugin 'variant.txt') @("variant=$Variant")
if ($Variant -eq 1) { Write-ProbeText (Join-Path $plugin 'only-in-variant-1.txt') @('removed by variant 2') }
else { Write-ProbeText (Join-Path $plugin 'only-in-variant-2.txt') @('added by variant 2') }

Add-Type -AssemblyName System.Drawing
$bitmap = [Drawing.Bitmap]::new(256, 256)
$graphics = [Drawing.Graphics]::FromImage($bitmap)
try {
    $graphics.Clear([Drawing.Color]::FromArgb(40, 60, 90))
    $graphics.DrawString("WLP test`r`nvariant $Variant", [Drawing.Font]::new('Arial', 20), [Drawing.Brushes]::White, 16, 90)
    $bitmap.Save((Join-Path $root 'source\preview.png'), [Drawing.Imaging.ImageFormat]::Png)
} finally { $graphics.Dispose(); $bitmap.Dispose() }

# Published layout, mirroring what Steam delivers for an APIShared package.
$simulated = Join-Path $root 'simulated'
$payload = Join-Path $simulated ('Payload\BepInEx\plugins\' + $ProbePayloadGuid)
[void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($payload))
Copy-Item -LiteralPath $plugin -Destination $payload -Recurse
$files = @(Get-ChildItem -LiteralPath (Join-Path $simulated 'Payload') -Recurse -File | Sort-Object FullName | ForEach-Object {
    [ordered]@{ Path = $_.FullName.Substring((Join-Path $simulated 'Payload').Length + 1).Replace('\', '/'); Size = $_.Length; Sha256 = Get-ProbeSha256 $_.FullName }
})
$manifest = [ordered]@{ SchemaVersion = 1; PackageId = $ProbePayloadGuid; Version = "0.1.$Variant"; MinimumAPISharedVersion = '0.56.0';
    MinimumScriptExtenderVersion = '2.14.0'; MaximumScriptExtenderVersion = ''; Roots = @("BepInEx/plugins/$ProbePayloadGuid"); Files = $files }
Write-ProbeText (Join-Path $simulated 'apishared-workshop.json') @($manifest | ConvertTo-Json -Depth 5)

Write-Output "Item root:      $root"
Write-Output "Upload source:  $(Join-Path $root 'source')"
Write-Output "Simulated item: $simulated (load key: simulated)"
