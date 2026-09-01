param(
    [ValidateSet('Validate', 'Knowledge', 'Resources', 'GhidraExports', 'Index', 'RestoreDatabase', 'All')]
    [string]$Stage = 'Validate'
)

$ErrorActionPreference = 'Stop'
$toolDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$baselineRoot = Split-Path -Parent (Split-Path -Parent $toolDirectory)
$workspace = Split-Path -Parent (Split-Path -Parent $baselineRoot)

$currentHash = 'FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2'
$managedHash = 'BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789'
$oldHash = '17F8DD4A92FF6125BD6A3A70ABC80C727682E489696C218D146A7EA6D2F88BF4'
$seCommit = '171d68e155a8f98c5f8c4ee154d9af154c9a2443'

$native = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll'
$managed = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Managed\Assembly-CSharp.dll'
$assets = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\sharedassets1.assets'
$oldNative = Join-Path $workspace 'x86_64\CrusaderDE.dll'
$seRoot = Join-Path $workspace 'shcde-script-extender'
$currentKey = $currentHash.Substring(0, 8)
$managedKey = $managedHash.Substring(0, 8)
$oldKey = $oldHash.Substring(0, 8)
if ($currentKey -eq $oldKey -and $currentHash -ne $oldHash) { throw "Native short-hash collision: $currentHash and $oldHash." }
$rawHashRoot = Join-Path $baselineRoot $currentHash
$semantic = Join-Path $baselineRoot "sem\$currentKey"
$managedDirectory = Join-Path $semantic "managed\$managedKey"
$comparison = Join-Path $baselineRoot "diff\${oldKey}-${currentKey}"
$database = Join-Path $semantic 'CrusaderDE-semantic.sqlite'
$databaseManifest = Join-Path $semantic 'DATABASE_INFO.json'
$currentIndex = Join-Path $baselineRoot 'CURRENT.json'
$python = 'D:\CDesktopLink\Portable\Python\WinPy64\python\python.exe'
$semanticTools = Join-Path $toolDirectory 'semantic_tools.py'
$databaseManifestTool = Join-Path $toolDirectory 'database_manifest.py'
$extractorProject = Join-Path $toolDirectory 'SemanticExtract\SemanticExtract.csproj'
$extractor = Join-Path $toolDirectory 'SemanticExtract\bin\Release\net10.0\SemanticExtract.exe'
$assetStudio = Join-Path $workspace '.tools\AssetStudio-2.4.1-net10\AssetStudio.CLI.exe'
$ghidra = Join-Path $workspace '.tools\ghidra-12.1.3\ghidra_12.1.3_PUBLIC\support\analyzeHeadless.bat'
$jdk = Join-Path $workspace '.tools\temurin-jdk-21.0.12.1+1\jdk-21.0.12.1+1'

function Assert-LastExitCode([string]$Operation) {
    if ($LASTEXITCODE -ne 0) { throw "$Operation failed with exit code $LASTEXITCODE." }
}

function Assert-Hash([string]$Path, [string]$Expected) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Required file is missing: $Path" }
    $actual = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
    if ($actual -ne $Expected) { throw "Hash mismatch for $Path. Expected $Expected, got $actual." }
}

function Initialize-Identity([string]$Path, [hashtable]$Expected) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        [IO.Directory]::CreateDirectory((Split-Path -Parent $Path)) | Out-Null
        $json = ($Expected | ConvertTo-Json -Depth 4) + "`r`n"
        [IO.File]::WriteAllText($Path, $json, [Text.UTF8Encoding]::new($false))
    }
    $identity = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    foreach ($name in $Expected.Keys) {
        if ($identity.$name -ne $Expected[$name]) {
            throw "Short-path identity collision at $Path for $name. Expected $($Expected[$name]), got $($identity.$name)."
        }
    }
}

function Invoke-DatabaseBuild([long]$CurrentSize, [long]$OldSize, [long]$ManagedSize, [string]$CurrentSource, [string]$OldSource, [string]$ManagedSource) {
    $indexArguments = @(
        'build-index', '--semantic-dir', $semantic, '--database', $database,
        '--current-hash', $currentHash, '--old-hash', $oldHash, '--managed-hash', $managedHash,
        '--current-native', $CurrentSource, '--old-native', $OldSource, '--managed-assembly', $ManagedSource,
        '--current-native-size', $CurrentSize, '--old-native-size', $OldSize, '--managed-size', $ManagedSize,
        '--old-exports', (Join-Path $comparison 'exports'), '--raw-exports', (Join-Path $rawHashRoot 'exports'),
        '--managed-dir', $managedDirectory, '--patterns', (Join-Path $semantic 'sources\pattern-matches.jsonl'),
        '--source-types', (Join-Path $semantic 'sources\source-types.jsonl'), '--type-fields', (Join-Path $semantic 'sources\type-fields.jsonl'),
        '--vtable-members', (Join-Path $semantic 'sources\vtable-members.jsonl'), '--delegates', (Join-Path $semantic 'sources\delegates.jsonl'),
        '--rtti-vtables', (Join-Path $semantic 'exports\rtti-vtables.jsonl'), '--xaml', (Join-Path $semantic 'resources\xaml-index.jsonl'),
        '--xaml-links', (Join-Path $semantic 'resources\xaml-managed-links.jsonl'), '--version-matches', (Join-Path $comparison 'version-matches.jsonl')
    )
    & $python $semanticTools @indexArguments
    Assert-LastExitCode 'SQLite index build'
}

function Invoke-DatabaseManifest([string]$Command) {
    & $python $databaseManifestTool $Command --baseline-root $baselineRoot --semantic $semantic --comparison $comparison --database $database --manifest $databaseManifest
    Assert-LastExitCode "Database manifest $Command"
}

Set-Location -LiteralPath $workspace
$semanticIdentityPath = Join-Path $semantic 'IDENTITY.json'
$comparisonIdentityPath = Join-Path $comparison 'IDENTITY.json'
if ($Stage -eq 'RestoreDatabase' -and (-not (Test-Path -LiteralPath $semanticIdentityPath -PathType Leaf) -or -not (Test-Path -LiteralPath $comparisonIdentityPath -PathType Leaf))) {
    throw 'RestoreDatabase requires both tracked IDENTITY.json files.'
}
Initialize-Identity $semanticIdentityPath @{ schemaVersion = 1; pathKey = $currentKey; currentNativeHash = $currentHash; managedPathKey = $managedKey; managedHash = $managedHash; scriptExtenderCommit = $seCommit }
Initialize-Identity $comparisonIdentityPath @{ schemaVersion = 1; pathKey = "${oldKey}-${currentKey}"; oldNativeHash = $oldHash; currentNativeHash = $currentHash }
if ($Stage -ne 'RestoreDatabase') {
    Assert-Hash $native $currentHash
    Assert-Hash $managed $managedHash
    Assert-Hash $oldNative $oldHash
    $actualCommit = (& git -C $seRoot rev-parse HEAD).Trim()
    Assert-LastExitCode 'Script Extender commit check'
    if ($actualCommit -ne $seCommit) { throw "Script Extender commit mismatch. Expected $seCommit, got $actualCommit." }
}

$runKnowledge = $Stage -in @('Knowledge', 'All')
$runResources = $Stage -in @('Resources', 'All')
$runGhidra = $Stage -in @('GhidraExports', 'All')
$runIndex = $Stage -in @('Index', 'All')

if ($runKnowledge) {
    dotnet restore $extractorProject
    Assert-LastExitCode 'SemanticExtract restore'
    dotnet build $extractorProject --configuration Release --no-restore
    Assert-LastExitCode 'SemanticExtract build'
    & $extractor source $seRoot (Join-Path $semantic 'sources') $seCommit
    Assert-LastExitCode 'Script Extender source extraction'
    & $extractor managed $managed (Join-Path $rawHashRoot 'exports\exports.jsonl') $managedDirectory $managedHash
    Assert-LastExitCode 'Managed metadata extraction'
    & ilspycmd -p -o (Join-Path $managedDirectory 'decompiled') $managed
    Assert-LastExitCode 'Assembly-CSharp decompilation'
    & $python $semanticTools managed-links --calls (Join-Path $managedDirectory 'managed-calls.jsonl') --pinvokes (Join-Path $managedDirectory 'pinvokes.jsonl') --output (Join-Path $managedDirectory 'managed-native-links.jsonl') --prototypes (Join-Path $semantic 'sources\pinvoke-prototypes.tsv')
    Assert-LastExitCode 'Managed/native linking'
    & $python $semanticTools scan-aobs --patterns (Join-Path $semantic 'sources\patterns.jsonl') --binary "$currentHash=$native" --binary "$oldHash=$oldNative" --current-hash $currentHash --output (Join-Path $semantic 'sources\pattern-matches.jsonl') --labels (Join-Path $semantic 'sources\aob-labels.tsv')
    Assert-LastExitCode 'AOB scan'
    & $python $semanticTools combine-headers --source (Join-Path $seRoot 'ReverseEngineering\structs') --destination (Join-Path $semantic 'sources\script-extender-types.h')
    Assert-LastExitCode 'Script Extender header copy'
    & $python $semanticTools sanitize-headers --source (Join-Path $semantic 'sources\headers') --output (Join-Path $semantic 'sources\script-extender-types-ghidra.h') --manifest (Join-Path $semantic 'sources\ghidra-header-manifest.jsonl')
    Assert-LastExitCode 'Ghidra header sanitization'
}

if ($runResources) {
    $rawDirectory = Join-Path $semantic 'resources\xaml-raw'
    $xamlDirectory = Join-Path $semantic 'resources\xaml'
    $names = @(Get-ChildItem -LiteralPath $rawDirectory -Filter '*.dat' | Sort-Object Name)
    if ($names.Count -eq 0) { throw 'The persistent XAML resource-name seed is empty.' }
    $staging = Join-Path ([IO.Path]::GetTempPath()) ("shcde-xaml-" + [Guid]::NewGuid().ToString('N'))
    [IO.Directory]::CreateDirectory($staging) | Out-Null
    try {
        foreach ($file in $names) {
            $name = [IO.Path]::GetFileNameWithoutExtension($file.Name)
            $pattern = '^' + [regex]::Escape($name) + '$'
            & $assetStudio $assets $staging --silent --game Normal --types MonoBehaviour --names $pattern --group_assets None --export_type Raw
            Assert-LastExitCode "AssetStudio extraction of $name"
        }
        $staged = @(Get-ChildItem -LiteralPath $staging -Filter '*.dat')
        if ($staged.Count -ne $names.Count) { throw "Expected $($names.Count) staged resources, got $($staged.Count)." }
        foreach ($file in $staged) { Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $rawDirectory $file.Name) -Force }
    }
    finally {
        if ([IO.Path]::GetFullPath($staging).StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()), [StringComparison]::OrdinalIgnoreCase)) {
            [IO.Directory]::Delete($staging, $true)
        }
    }
    $datProject = Join-Path $semantic 'sources\Dat2XAML-source\SHCDESE.Dat2XAML.csproj'
    dotnet restore $datProject
    Assert-LastExitCode 'Dat2XAML restore'
    dotnet build $datProject --configuration Debug --no-restore
    Assert-LastExitCode 'Dat2XAML build'
    $datExe = Join-Path $semantic 'sources\Dat2XAML-source\bin\net10.0\SHCDESE.Dat2XAML.exe'
    foreach ($file in $names) { & $datExe $file.FullName $xamlDirectory; Assert-LastExitCode "Dat2XAML conversion of $($file.Name)" }
    & $python $semanticTools xaml --xaml-root $xamlDirectory --managed-methods (Join-Path $managedDirectory 'managed-methods.jsonl') --output (Join-Path $semantic 'resources\xaml-index.jsonl') --links (Join-Path $semantic 'resources\xaml-managed-links.jsonl')
    Assert-LastExitCode 'XAML index'
}

if ($runGhidra) {
    $env:JAVA_HOME = $jdk
    $currentProject = Join-Path $semantic 'ghidra'
    $currentExports = Join-Path $semantic 'exports'
    & $ghidra $currentProject 'CrusaderDE-Semantic' -process 'CrusaderDE.dll' -noanalysis -scriptPath $toolDirectory -postScript ApplyCrusaderSemantics.java (Join-Path $semantic 'sources\aob-labels.tsv') (Join-Path $semantic 'sources\pinvoke-prototypes.tsv') (Join-Path $semantic 'sources\script-extender-types-ghidra.h') (Join-Path $semantic 'sources\CrusaderDE-ScriptExtender.gdt') (Join-Path $currentExports 'applied-labels.json')
    Assert-LastExitCode 'Current semantic Ghidra apply'
    & $ghidra $currentProject 'CrusaderDE-Semantic' -process 'CrusaderDE.dll' -noanalysis -scriptPath $toolDirectory -postScript ExportCrusaderSemantics.java $currentExports $currentHash
    Assert-LastExitCode 'Current semantic Ghidra export'
    & $ghidra (Join-Path $comparison 'ghidra') 'CrusaderDE-Historical' -process 'CrusaderDE.dll' -noanalysis -scriptPath $toolDirectory -postScript ExportCrusaderSemantics.java (Join-Path $comparison 'exports') $oldHash
    Assert-LastExitCode 'Historical Ghidra export'
}

if ($runIndex) {
    & $python $semanticTools compare --old (Join-Path $comparison 'exports\semantic-functions.jsonl') --new (Join-Path $semantic 'exports\semantic-functions.jsonl') --output $comparison
    Assert-LastExitCode 'Version comparison'
    & $python $semanticTools xaml --xaml-root (Join-Path $semantic 'resources\xaml') --managed-methods (Join-Path $managedDirectory 'managed-methods.jsonl') --output (Join-Path $semantic 'resources\xaml-index.jsonl') --links (Join-Path $semantic 'resources\xaml-managed-links.jsonl')
    Assert-LastExitCode 'XAML index'
    Invoke-DatabaseBuild (Get-Item -LiteralPath $native).Length (Get-Item -LiteralPath $oldNative).Length (Get-Item -LiteralPath $managed).Length $native $oldNative $managed
    & $python $databaseManifestTool create --baseline-root $baselineRoot --semantic $semantic --comparison $comparison --database $database --manifest $databaseManifest --raw-root $rawHashRoot --managed-dir $managedDirectory --current-hash $currentHash --managed-hash $managedHash --old-hash $oldHash --se-commit $seCommit --current-index $currentIndex
    Assert-LastExitCode 'Database manifest creation'
    Invoke-DatabaseManifest 'validate'
}

if ($Stage -eq 'RestoreDatabase') {
    Invoke-DatabaseManifest 'verify-inputs'
    $manifestData = Get-Content -LiteralPath $databaseManifest -Raw | ConvertFrom-Json
    $currentBinary = $manifestData.binaries | Where-Object role -eq 'current-native'
    $oldBinary = $manifestData.binaries | Where-Object role -eq 'historical-native'
    $managedBinary = $manifestData.binaries | Where-Object role -eq 'current-managed'
    if (-not $currentBinary -or -not $oldBinary -or -not $managedBinary) { throw 'DATABASE_INFO.json does not contain all three binary identities.' }
    Invoke-DatabaseBuild $currentBinary.size $oldBinary.size $managedBinary.size $currentBinary.sourcePath $oldBinary.sourcePath $managedBinary.sourcePath
    Invoke-DatabaseManifest 'validate'
}

if ($Stage -in @('Validate', 'All')) {
    Invoke-DatabaseManifest 'validate'
    & $python (Join-Path $toolDirectory 'validate.py') --semantic $semantic --comparison $comparison --baseline-root $baselineRoot --database (Join-Path $semantic 'CrusaderDE-semantic.sqlite') --native $native --managed $managed --old-native $oldNative --raw-root $rawHashRoot --raw-before (Join-Path $semantic 'validation\raw-baseline-before.jsonl') --se-root $seRoot --se-before (Join-Path $semantic 'validation\script-extender-before.jsonl') --current-hash $currentHash --managed-hash $managedHash --old-hash $oldHash --output (Join-Path $semantic 'validation\validation-report.json')
    Assert-LastExitCode 'Semantic validation'
    $env:JAVA_HOME = $jdk
    & $ghidra (Join-Path $semantic 'ghidra') 'CrusaderDE-Semantic' -process 'CrusaderDE.dll' -readOnly -noanalysis -scriptPath (Join-Path $baselineRoot 'tools') -postScript ValidateCrusaderBaseline.java -log (Join-Path $semantic 'logs\ghidra-readonly-validation.log')
    Assert-LastExitCode 'Current Ghidra read-only validation'
    & $ghidra (Join-Path $comparison 'ghidra') 'CrusaderDE-Historical' -process 'CrusaderDE.dll' -readOnly -noanalysis -scriptPath (Join-Path $baselineRoot 'tools') -postScript ValidateCrusaderBaseline.java -log (Join-Path $comparison 'logs\ghidra-readonly-validation.log')
    Assert-LastExitCode 'Historical Ghidra read-only validation'
}
