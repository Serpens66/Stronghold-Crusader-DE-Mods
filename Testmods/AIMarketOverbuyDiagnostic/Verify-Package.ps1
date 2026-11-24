param([Parameter(Mandatory=$true)][string]$AssemblyPath,[switch]$DevDiag)
$ErrorActionPreference='Stop'
$game='E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
[void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $game 'BepInEx\core\Mono.Cecil.dll')))
$assembly=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($AssemblyPath)
try {
    $types=@($assembly.MainModule.Types | ForEach-Object FullName)
    foreach($name in @('MarketPendingDemandFix','MarketDemandTarget','MarketDemandSaveCodec','MarketDemandMissionState','MarketDemandWriterHooks','MarketDemandSites','MarketAmmoDemandSource','MarketFixesDemandSource')) {
        if($types -notcontains ('AIMarketOverbuyDiagnostic.'+$name)) { throw "Functional fix missing from package: $name" }
    }
    foreach($name in @('MarketObservation','AIMarketOverbuyDiagnosticRuntime','MarketTradeTrace','MarketDemandEvidence','MarketAutomation','MarketBacklogAic')) {
        $found=$types -contains ('AIMarketOverbuyDiagnostic.'+$name)
        if($found -ne [bool]$DevDiag) { throw "Diagnostic package boundary violated: $name present=$found devdiag=$DevDiag" }
    }
    $marked=@($assembly.CustomAttributes | Where-Object { $_.AttributeType.FullName -eq 'System.Reflection.AssemblyMetadataAttribute' -and $_.ConstructorArguments[0].Value -eq 'SerpDevDiagnostics' }).Count -ne 0
    if($marked -ne [bool]$DevDiag) { throw 'Diagnostic assembly marker does not match requested package.' }
    Write-Host "PASS: functional core present; optional diagnostics=$([bool]$DevDiag)."
} finally { $assembly.Dispose() }
