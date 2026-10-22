[CmdletBinding()]
param([string]$GameDir = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition',
    [string]$MSBuild)
$ErrorActionPreference = 'Stop'
$workspaceNk = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
$sourcesNk = Join-Path $workspaceNk 'Findings\src\NativeKnowledge'
. (Join-Path $workspaceNk 'Shared\Tools\Paths\WorkPaths.ps1')
. (Join-Path $workspaceNk 'Shared\Tools\ApiSharedRepository\RepositoryProcess.Common.ps1')
$workNk = New-ShortWorkDirectory -Root $workspaceNk -Kind t -Name native-knowledge -Identity 'Staged NativeKnowledge standalone verification'
Write-Output ('Verification artifacts: ' + $workNk)

function Invoke-CheckedNk([string]$Program, [string[]]$Arguments, [string]$Log) {
    $outputNk = @(& $Program @Arguments 2>&1)
    $exitNk = $LASTEXITCODE
    $textNk = ($outputNk | ForEach-Object { [string]$_ }) -join "`r`n"
    [IO.File]::WriteAllText($Log, $textNk + "`r`n", [Text.UTF8Encoding]::new($false))
    Write-Output $textNk
    if ($exitNk -ne 0) { throw ('Verification command failed (' + $exitNk + '): ' + $Program + '; see ' + $Log) }
}

try {
    if (-not $MSBuild) {
        $locatorNk = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe'
        if (-not (Test-Path -LiteralPath $locatorNk -PathType Leaf)) { throw 'Supply -MSBuild or install the VS locator.' }
        $MSBuild = @(& $locatorNk -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe') | Select-Object -First 1
    }
    if (-not (Test-Path -LiteralPath $MSBuild -PathType Leaf)) { throw 'MSBuild not found.' }

    # Guardrails are deliberately limited to this staged source set. They are not
    # a substitute for behavior/layout tests or proof of in-game lifetime safety.
    $inputFilesNk = @(Get-ChildItem -LiteralPath $sourcesNk -File) +
        @(Get-ChildItem -LiteralPath $PSScriptRoot -File | Where-Object { $_.Extension -in @('.cs','.csproj','.ps1','.py','.json') })
    $fingerprintsNk = @()
    foreach ($fileNk in $inputFilesNk) {
        $textNk = [IO.File]::ReadAllText($fileNk.FullName)
        if ([regex]::IsMatch($textNk, '(?<!\r)\n') -or [regex]::IsMatch($textNk, '\r(?!\n)')) {
            throw ('Non-CRLF text: ' + $fileNk.FullName)
        }
        $fingerprintsNk += [pscustomobject]@{ Path=$fileNk.FullName.Substring($workspaceNk.Length + 1); Sha256=(Get-ApiRepositoryHash $fileNk.FullName) }
        if ($fileNk.DirectoryName -eq $sourcesNk -and $fileNk.Extension -eq '.cs') {
            $forbiddenNk = '\b(Newtonsoft|JsonUtility|JavaScriptSerializer|DataContractJsonSerializer|BaseUnityPlugin|MonoBehaviour|StartCoroutine|VirtualProtect|NativeDetour|CodePatch)\b|System\.Text\.Json|System\.Web\.Extensions|\bvoid\s+(OnDestroy|OnDisable|OnApplicationQuit|Update|LateUpdate|FixedUpdate)\s*\('
            if ([regex]::IsMatch($textNk, $forbiddenNk)) { throw ('Unexpected runtime mechanism/dependency: ' + $fileNk.Name) }
        }
    }

    # Fresh Windows PowerShell is part of the test: the installed-interop path must
    # work without assemblies/modules leaked from an interactive PS7 session.
    Invoke-CheckedNk 'powershell.exe' @('-NoProfile','-ExecutionPolicy','Bypass','-File',
        (Join-Path $PSScriptRoot 'Verify-Interop.ps1'),'-GameDir',$GameDir) (Join-Path $workNk 'interop.log')
    Invoke-CheckedNk 'dotnet' @('build',(Join-Path $PSScriptRoot 'NativeKnowledge.Tests.csproj'),
        '--artifacts-path',(Join-Path $workNk 'cli'),'-v:minimal') (Join-Path $workNk 'cli-build.log')
    Invoke-CheckedNk 'dotnet' @((Join-Path $workNk 'cli\bin\NativeKnowledge.Tests\debug\NativeKnowledge.Tests.dll')) (Join-Path $workNk 'cli-tests.log')
    Invoke-CheckedNk $MSBuild @((Join-Path $PSScriptRoot 'NativeKnowledge.Framework.csproj'),'/t:Build','/p:Configuration=Release',
        ('/p:OutputPath=' + (Join-Path $workNk 'framework\bin') + '/'),
        ('/p:IntermediateOutputPath=' + (Join-Path $workNk 'framework\obj') + '/'),
        ('/p:ExtenderDir=' + (Join-Path $GameDir 'BepInEx\plugins\000shcdese')),'/v:minimal','/nologo') (Join-Path $workNk 'framework-build.log')
    Invoke-CheckedNk (Join-Path $workNk 'framework\bin\NativeKnowledge.FrameworkCheck.exe') @() (Join-Path $workNk 'framework-tests.log')
    # Do not label results as current when parallel edits changed a captured input.
    foreach ($fingerprintNk in $fingerprintsNk) {
        if ((Get-ApiRepositoryHash (Join-Path $workspaceNk $fingerprintNk.Path)) -cne $fingerprintNk.Sha256) { throw 'Verification input changed during the run.' }
    }
    $resultNk = [ordered]@{ Status='passed'; CompletedUtc=[DateTime]::UtcNow.ToString('o');
        Source='Findings/src/NativeKnowledge'; InstalledExtenderVersion=[Reflection.AssemblyName]::GetAssemblyName((Join-Path $GameDir 'BepInEx\plugins\000shcdese\SHCDESE.dll')).Version.ToString();
        InstalledExtenderSha256=(Get-ApiRepositoryHash (Join-Path $GameDir 'BepInEx\plugins\000shcdese\SHCDESE.dll'));
        NativeHash=(Get-ApiRepositoryHash (Join-Path $GameDir 'Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll'));
        Checks=@('scoped-source-guardrails','CRLF','installed-native-and-interop','net10-production-behavior','net481-production-behavior-and-XML-docs');
        GameplayAcceptance=$false; ApiSharedChanged=$false; Inputs=$fingerprintsNk }
    [IO.File]::WriteAllText((Join-Path $workNk 'result.json'), (($resultNk | ConvertTo-Json -Depth 6).Replace("`r`n","`n").Replace("`n","`r`n") + "`r`n"), [Text.UTF8Encoding]::new($false))
    Write-Output 'PASS: isolated checks completed. APIShared and the installed game were not changed.'
} catch {
    [IO.File]::WriteAllText((Join-Path $workNk 'failure.txt'), $_.ToString() + "`r`n", [Text.UTF8Encoding]::new($false))
    throw
}
