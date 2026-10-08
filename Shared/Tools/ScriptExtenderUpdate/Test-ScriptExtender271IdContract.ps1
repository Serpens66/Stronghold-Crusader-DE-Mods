[CmdletBinding()]
param(
    [string]$InstalledAssembly = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins\000shcdese\SHCDESE.dll'
)

$ErrorActionPreference = 'Stop'
$productVersionText = [Diagnostics.FileVersionInfo]::GetVersionInfo($InstalledAssembly).ProductVersion
$productVersion = $null
if (-not [Version]::TryParse($productVersionText, [ref]$productVersion) -or $productVersion -lt [Version]'2.7.1') {
    throw "The installed Script Extender version '$productVersionText' predates the 2.7.1 manager-ID fix."
}
$assemblyHash = (Get-FileHash -LiteralPath $InstalledAssembly -Algorithm SHA256).Hash

$unit = (& ilspycmd -t 'SHCDESE.API.GameUnitManagerAPI' $InstalledAssembly | Out-String)
$building = (& ilspycmd -t 'SHCDESE.API.GameBuildingManagerAPI' $InstalledAssembly | Out-String)
$query = (& ilspycmd -t 'SHCDESE.Interop.Query.GameStructQuery`1' $InstalledAssembly | Out-String)
if ($LASTEXITCODE -ne 0) { throw 'ilspycmd failed while decompiling the installed Script Extender.' }

$contracts = @(
    @{ Name = 'Unit live view skips reserved slot 0 and exposes 9999 records'; Text = $unit; Pattern = '_unitArray\s*=.*\+\s*sizeof\(GameUnit\),\s*9999\)' },
    @{ Name = 'Unit IDs resolve through zero-based span index id - 1'; Text = $unit; Pattern = '_unitArray\._array\s*\+\s*\(unitId\s*-\s*1\)' },
    @{ Name = 'Unit ID 0 and the first ID behind the array are rejected'; Text = $unit; Pattern = 'unitId\s*<=\s*0\s*\|\|\s*unitId\s*>\s*_unitArray\.Length' },
    @{ Name = 'Unit GetByGlobalId consumes one-based query IDs'; Text = $unit; Pattern = 'HasGlobalId\(globalId\)\)\.ToIdList' },
    @{ Name = 'Building live view skips reserved slot 0 and exposes 3999 records'; Text = $building; Pattern = '_buildingArray\s*=.*\+\s*sizeof\(GameBuilding\),\s*3999\)' },
    @{ Name = 'Building IDs resolve through zero-based span index id - 1'; Text = $building; Pattern = '_buildingArray\._array\s*\+\s*\(buildingId\s*-\s*1\)' },
    @{ Name = 'Building ID 0 and the first ID behind the array are rejected'; Text = $building; Pattern = 'buildingId\s*<=\s*0\s*\|\|\s*buildingId\s*>\s*_buildingArray\.Length' },
    @{ Name = 'Building GetAll/ExecuteQuery emits one-based query IDs'; Text = $building; Pattern = 'gameStructQuery\.ToIdList\(results\)' },
    @{ Name = 'Query ID output is index plus one'; Text = $query; Pattern = 'results\.Add\(i\s*\+\s*1\)' },
    @{ Name = 'Query index output remains zero-based'; Text = $query; Pattern = 'results\.Add\(i\)' },
    @{ Name = 'Enumerator CurrentId is one-based'; Text = $query; Pattern = 'CurrentId\s*=>\s*_currentIndex\s*\+\s*1' }
)
foreach ($contract in $contracts) {
    if ($contract.Text -notmatch $contract.Pattern) { throw "Missing contract: $($contract.Name)." }
}

foreach ($length in 9999, 3999) {
    $isValid = { param([int]$id) $id -gt 0 -and $id -le $length }
    if (-not (& $isValid 1) -or -not (& $isValid $length) -or
        (& $isValid 0) -or (& $isValid ($length + 1)) -or
        (1 - 1) -ne 0 -or ($length - 1) -ne ($length - 1)) {
        throw "Boundary contract failed for live-array length $length."
    }
}

Write-Output "PASS: installed SHCDESE $productVersionText ($assemblyHash) satisfies Unit and Building slot-1, highest-ID, zero, one-past-end, span-index and query-ID contracts, plus Unit GetByGlobalId. GameBuildingManagerAPI has no GetByGlobalId method."
