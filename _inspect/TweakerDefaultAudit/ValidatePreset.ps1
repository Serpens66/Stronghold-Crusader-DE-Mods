$ErrorActionPreference = 'Stop'
$configRoot = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\config\CrusaderDETweaker'
$preset = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'Override\CrusaderDETweaker\preset_default-reset-check.json') | ConvertFrom-Json
if ($preset.targetGuid -ne 'CrusaderDETweaker' -or $preset.schemaVersion -ne 1) { throw 'Unexpected preset contract' }
$count = 0
foreach ($option in $preset.settings.PSObject.Properties) {
    $parts = $option.Name.Split('/')
    if ($parts.Length -ne 3) { throw "Unexpected test key: $($option.Name)" }
    $section = [Uri]::UnescapeDataString($parts[1])
    $key = [Uri]::UnescapeDataString($parts[2])
    $csv = Join-Path $configRoot ('DamageMatrices\' + $parts[0] + '.csv')
    $found = $false
    if (Test-Path -LiteralPath $csv) {
        $rows = @(Get-Content -LiteralPath $csv | Where-Object { $_.Trim() -and -not $_.TrimStart().StartsWith('#') })
        $columns = @($rows[0].Split(',') | ForEach-Object { $_.Trim() })
        $found = $columns -contains $key -and @($rows | Select-Object -Skip 1 | Where-Object { $_.Split(',')[0].Trim() -ceq $section }).Count -eq 1
    } else {
        $path = Join-Path $configRoot ($parts[0] + '.toml')
        if (!(Test-Path -LiteralPath $path)) { $path = Join-Path $configRoot ($parts[0] + '.cfg') }
        $currentSection = ''
        foreach ($line in Get-Content -LiteralPath $path) {
            if ($line -match '^\s*\[(.+)\]\s*$') { $currentSection = $Matches[1].Trim('"'); continue }
            if ($currentSection -ceq $section -and $line -match '^\s*([^#=]+?)\s*=') {
                if ($Matches[1].Trim() -ceq $key) { $found = $true }
            }
        }
    }
    if (!$found) { throw "Unknown test-preset option: $($option.Name)" }
    if ($option.Value.mode -ne 'fixed') { throw 'Expected fixed test values' }
    $count++
}
if ($count -ne 4) { throw 'Expected exactly four test changes' }
Write-Output 'PASS: all four test-preset keys exist in the installed configuration documents.'
