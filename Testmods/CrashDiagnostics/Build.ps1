[CmdletBinding()]
param(
    [switch]$DevDiag,
    [switch]$VerifyOnly,
    [string]$GameDir = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition',
    [string]$MSBuild = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $workspace 'Shared/Tools/Release/Release.Common.ps1')
. (Join-Path $workspace 'Shared/Tools/Paths/WorkPaths.ps1')
. (Join-Path $workspace 'Shared/Tools/BuildCache/MsvcToolchain.ps1')
. (Join-Path $workspace 'Shared/Tools/ApiSharedRepository/MainSource.Common.ps1')
$selected = Join-Path $workspace '.work/c/apishared-main.selection.json'
$proofPath = Join-Path $workspace '.release-output/APIShared/cache/v0.53.0/APIShared-v0.53.0.provenance.json'
if (-not (Test-Path -LiteralPath $selected) -or -not (Test-Path -LiteralPath $proofPath)) {
    throw 'The verified APIShared 0.53.0 main release cache is required. Prepare it through the canonical release SDK workflow; this driver never changes/rebuilds the SDK.'
}
$selection = Assert-ApiMainSelection $workspace $selected
$proof = Get-Content -LiteralPath $proofPath -Raw | ConvertFrom-Json
$sdk = Join-Path (Split-Path -Parent $proofPath) 'audit/BepInEx/plugins/APIShared_Serp'
$envNames = @('SHCDE_API_SHARED_MAIN_SELECTION','SHCDE_API_SHARED_RELEASE_PROOF','SHCDE_API_SHARED_RELEASE_PROOF_SHA256','SHCDE_API_SHARED_DIR','SHCDE_BRANCH_API_ROOT','SHCDE_BRANCH_API_COMMIT','SHCDE_DEVDIAG','INCLUDE','LIB')
$saved = @{}
foreach ($name in $envNames) { $saved[$name] = [Environment]::GetEnvironmentVariable($name,'Process') }
$started = [DateTime]::UtcNow
$obj = Join-Path $PSScriptRoot 'obj'
$staging = Join-Path $obj 'stage'
$output = Join-Path $PSScriptRoot 'BepInEx/plugins/CrashDiagnostics_Serp'
$testRun = $null
try {
    $env:SHCDE_API_SHARED_MAIN_SELECTION = $selected
    $env:SHCDE_API_SHARED_RELEASE_PROOF = $proofPath
    $env:SHCDE_API_SHARED_RELEASE_PROOF_SHA256 = Get-Sha256Hex -Path $proofPath
    $env:SHCDE_API_SHARED_DIR = $sdk
    # Source-based preflights must inspect the same validated main SDK as the build,
    # even while another chat develops a newer SDK in the personal checkout.
    $env:SHCDE_BRANCH_API_ROOT = $selection.Root
    $env:SHCDE_BRANCH_API_COMMIT = $selection.Commit
    $env:SHCDE_DEVDIAG = if ($DevDiag) { '1' } else { '' }
    & (Join-Path $PSScriptRoot 'verify.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'Source preflight failed.' }
    if ($VerifyOnly) { return }
    if ($DevDiag -and ((& git -C $workspace branch --show-current) -join '').Trim() -ceq 'main') { throw 'Diagnostic builds must not be built from the main working branch. Release SDK references remain main.' }
    $inputFiles = @(Get-ChildItem -LiteralPath $PSScriptRoot -Recurse -File | Where-Object {
        $_.FullName -notmatch '[\\/](obj|bin|BepInEx|__pycache__)[\\/]' -and $_.Extension -in @('.cs','.csproj','.cpp','.h','.ps1','.bat','.json','.txt','.md','.py')
    } | ForEach-Object { $_.FullName })
    $inputFiles += @('Shared/Runtime/Persistence/DependencyFreeJson.cs','Shared/Runtime/Diagnostics/DevDiag.cs','Directory.Build.targets','Shared/Tools/BuildCache/MsvcToolchain.ps1') | ForEach-Object { Join-Path $workspace $_ }
    $inputHashes = @($inputFiles | Sort-Object -Unique | ForEach-Object { [ordered]@{Path=$_.Substring($workspace.Length+1).Replace('\','/');Sha256=(Get-Sha256Hex -Path $_)} })
    $nativeToolchain = $null
    [void][IO.Directory]::CreateDirectory($obj)
    if (Test-Path -LiteralPath (Join-Path $obj 'build.json')) { Remove-Item -LiteralPath (Join-Path $obj 'build.json') -Force }
    # Only this fixed, generated directory may be replaced; never follow links.
    foreach ($target in @($staging,$output)) {
        $full = [IO.Path]::GetFullPath($target)
        if (-not $full.StartsWith($PSScriptRoot+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Output escapes the mod directory.' }
        if (Test-Path -LiteralPath $target) {
            foreach ($entry in @(Get-Item -LiteralPath $target) + @(Get-ChildItem -LiteralPath $target -Recurse -Force)) {
                if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked build output is not replaceable.' }
            }
        }
    }
    if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
    [void][IO.Directory]::CreateDirectory($staging)
    $mode = if ($DevDiag) { 'diagnostic' } else { 'normal' }
    $normalOutput = Join-Path $obj 'normal'
    if ($DevDiag) {
        [void][IO.Directory]::CreateDirectory($normalOutput)
        & $MSBuild (Join-Path $PSScriptRoot 'CrashDiagnostics.csproj') /t:Rebuild /nologo /verbosity:minimal /p:Configuration=Release "/p:GameDir=$GameDir" "/p:ApiSharedDir=$sdk" "/p:OutputPath=$normalOutput/" "/p:IntermediateOutputPath=$obj/managed-normal/" /p:SerpDevDiagnostics=false
        if ($LASTEXITCODE) { throw 'Normal no-op compilation failed.' }
    }
    & $MSBuild (Join-Path $PSScriptRoot 'CrashDiagnostics.csproj') /t:Rebuild /nologo /verbosity:minimal /p:Configuration=Release "/p:GameDir=$GameDir" "/p:ApiSharedDir=$sdk" "/p:OutputPath=$staging/" "/p:IntermediateOutputPath=$obj/managed-$mode/" "/p:SerpDevDiagnostics=$($DevDiag.IsPresent.ToString().ToLowerInvariant())"
    if ($LASTEXITCODE) { throw 'Managed compilation failed.' }
    if ($DevDiag) {
        $toolchain = Get-MsvcToolchain
        $nativeToolchain = [ordered]@{CompilerRoot=$toolchain.CompilerRoot;Sdk=$toolchain.Sdk.Name;CompilerSha256=(Get-Sha256Hex -Path (Join-Path $toolchain.CompilerRoot 'bin/Hostx64/x64/cl.exe'))}
        $env:INCLUDE = $toolchain.IncludeDirectories -join ';'
        $env:LIB = $toolchain.LibraryDirectories -join ';'
        $compiler = Join-Path $toolchain.CompilerRoot 'bin/Hostx64/x64/cl.exe'
        $nativeObj = Join-Path $obj 'native'
        [void][IO.Directory]::CreateDirectory($nativeObj)
        & $compiler /nologo /std:c++17 /O2 /MT /W4 /LD (Join-Path $PSScriptRoot 'native/Native.cpp') "/Fo$nativeObj/Native.obj" /link "/OUT:$staging/CrashDiagnostics.Native.dll" "/PDB:$staging/CrashDiagnostics.Native.pdb" /DEBUG /INCREMENTAL:NO "/IMPLIB:$nativeObj/CrashDiagnostics.Native.lib"
        if ($LASTEXITCODE) { throw 'Native capture compilation failed.' }
        & $compiler /nologo /std:c++17 /O2 /MT /EHsc /W4 (Join-Path $PSScriptRoot 'collector/Collector.cpp') "/Fo$nativeObj/Collector.obj" /link "/OUT:$staging/CrashDiagnostics.Collector.exe" "/PDB:$staging/CrashDiagnostics.Collector.pdb" /DEBUG /INCREMENTAL:NO /SUBSYSTEM:CONSOLE
        if ($LASTEXITCODE) { throw 'Collector compilation failed.' }
        & $compiler /nologo /std:c++17 /O2 /MT /EHsc /W4 (Join-Path $PSScriptRoot 'tests/native/TestHost.cpp') "/Fo$nativeObj/TestHost.obj" /link "/OUT:$nativeObj/CrashDiagnostics.TestHost.exe"
        if ($LASTEXITCODE) { throw 'Native test host compilation failed.' }
        & $compiler /nologo /std:c++17 /O2 /MT /EHsc /W4 (Join-Path $PSScriptRoot 'collector/Collector.Tests.cpp') "/Fo$nativeObj/Collector.Tests.obj" /link "/OUT:$nativeObj/CrashDiagnostics.Collector.Tests.exe"
        if ($LASTEXITCODE) { throw 'Collector regression compilation failed.' }
        $testRun = New-ShortWorkDirectory -Root $workspace -Kind t -Name crashdiag -Identity 'native capture and collector regression'
        & (Join-Path $PSScriptRoot 'tests/Run-Tests.ps1') -TestHost (Join-Path $nativeObj 'CrashDiagnostics.TestHost.exe') -Package $staging -OutputDirectory $testRun -GameDir $GameDir -CollectorTests (Join-Path $nativeObj 'CrashDiagnostics.Collector.Tests.exe')
        if ($LASTEXITCODE) { throw 'Native/collector regression failed.' }
        & (Join-Path $PSScriptRoot 'tests/Managed.Tests/Verify-Assemblies.ps1') -NormalAssembly (Join-Path $normalOutput 'CrashDiagnostics.dll') -DiagnosticAssembly (Join-Path $staging 'CrashDiagnostics.dll') -NativeLibrary (Join-Path $staging 'CrashDiagnostics.Native.dll') -GameDir $GameDir
        if ($LASTEXITCODE) { throw 'Normal/diagnostic artifact boundary verification failed.' }
    }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'info.json') -Destination $staging
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Installation.txt') -Destination $staging
    foreach ($inputRecord in $inputHashes) {
        if ((Get-Sha256Hex -Path (Join-Path $workspace $inputRecord.Path)) -cne $inputRecord.Sha256) { throw ('Build input changed during compilation: '+$inputRecord.Path) }
    }
    # Publish only after all relevant checks passed. The previous package is not success proof.
    if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Recurse -Force }
    [void][IO.Directory]::CreateDirectory($output)
    Get-ChildItem -LiteralPath $staging -File | Where-Object { $_.Extension -in @('.dll','.exe','.pdb') -or $_.Name -in @('info.json','Installation.txt') } | Copy-Item -Destination $output
    $files = @(Get-ChildItem -LiteralPath $output -File | ForEach-Object { [ordered]@{Path=$_.Name;Size=$_.Length;Sha256=(Get-Sha256Hex -Path $_.FullName)} })
    $record = [ordered]@{SchemaVersion=1;Result='passed';Mode=$mode;StartedUtc=$started.ToString('o');CompletedUtc=[DateTime]::UtcNow.ToString('o');SourceHead=((& git -C $workspace rev-parse HEAD)-join '').Trim();SourceBranch=((& git -C $workspace branch --show-current)-join '').Trim();ReleaseSdkCommit=$selection.Commit;ReleaseSdkVersion=$proof.Version;ReleaseSdkProofSha256=(Get-Sha256Hex -Path $proofPath);NativeToolchain=$nativeToolchain;Inputs=$inputHashes;Installation='not installed';Proton='unverified pilot';Multiplayer='not run';Files=$files}
    [IO.File]::WriteAllText((Join-Path $obj 'build.json'),(($record | ConvertTo-Json -Depth 8)-replace '\r?\n',"`r`n")+"`r`n",[Text.UTF8Encoding]::new($false))
    if ($DevDiag) {
        Add-Type -AssemblyName System.IO.Compression
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $archive = Join-Path $PSScriptRoot 'CrashDiagnostics-diagnostic-0.1.1.zip'
        $zipStage = Join-Path $obj 'zip'
        if (Test-Path -LiteralPath $zipStage) {
            if ((Get-Item -LiteralPath $zipStage).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked ZIP staging.' }
            Remove-Item -LiteralPath $zipStage -Recurse -Force
        }
        $zipPlugin = Join-Path $zipStage 'BepInEx/plugins/CrashDiagnostics_Serp'
        [void][IO.Directory]::CreateDirectory($zipPlugin)
        Get-ChildItem -LiteralPath $output -File | Copy-Item -Destination $zipPlugin
        if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive -Force }
        [IO.Compression.ZipFile]::CreateFromDirectory($zipStage,$archive)
        Write-Host "Diagnostic pilot ZIP: $archive"
    }
    Write-Host "PASS: CrashDiagnostics $mode build; released SDK/mod installation unchanged."
} catch {
    [void][IO.Directory]::CreateDirectory($obj)
    $details=[Collections.Generic.List[object]]::new()
    if ($testRun -and (Test-Path -LiteralPath $testRun)) {
        foreach ($file in Get-ChildItem -LiteralPath $testRun -Recurse -File | Where-Object { $_.Length -le 65536 -and $_.Name -match '(\.out\.txt|\.err\.txt|report\.txt|collector-status\.txt)$' } | Select-Object -First 32) {
            $details.Add(@{Name=$file.FullName.Substring($testRun.Length);Text=[IO.File]::ReadAllText($file.FullName)})
        }
    }
    $failure=@{Result='failed';Utc=[DateTime]::UtcNow.ToString('o');Error=$_.Exception.Message;Stack=$_.ScriptStackTrace;TestDetails=$details}
    [IO.File]::WriteAllText((Join-Path $obj 'build-failure.json'),(($failure|ConvertTo-Json -Depth 7)-replace '\r?\n',"`r`n")+"`r`n",[Text.UTF8Encoding]::new($false))
    throw
} finally {
    foreach ($name in $envNames) { [Environment]::SetEnvironmentVariable($name,$saved[$name],'Process') }
    if ($testRun -and (Test-Path -LiteralPath $testRun)) { Remove-ShortWorkDirectory -Path $testRun }
}
