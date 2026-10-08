param([string]$ModName)

. (Join-Path $PSScriptRoot 'Release.Common.ps1')

try {
    $report = @(Get-SetupReport -ModName $ModName)
    $report | Format-Table -AutoSize
    if (@($report | Where-Object { -not $_.Ok }).Count -gt 0) {
        Write-Host "`r`nSetup check failed." -ForegroundColor Red
        exit 1
    }
    Write-Host "`r`nSetup check succeeded." -ForegroundColor Green
    exit 0
} catch {
    Write-Error $_
    exit 1
}
