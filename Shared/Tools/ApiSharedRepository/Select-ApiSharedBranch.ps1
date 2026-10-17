[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Workspace,
    [Parameter(Mandatory)][string]$OutFile,
    [Parameter(ValueFromRemainingArguments)][string[]]$DriverArguments
)
# Selects the APIShared source a consumer build.bat compiles against.
#   /apishared:main  pinned public main (release branch), isolated worktree under
#                    .release-output/APIShared/main/<commit>/source, built with /noinstall
#   /apishared:dev   local development checkout APIShared/ (its current branch)
# SHCDE_API_SHARED_BRANCH supplies the same choice for scripted builds.
# The caller applies the result through the generated cmd file; nothing is installed here.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ApiShared.Common.ps1')

function Write-Selection([string[]]$Lines) {
    $text = (($Lines + '') -join "`r`n")
    # cmd reads the file in its OEM code page; refuse paths that ASCII cannot carry losslessly.
    if ($text -match '[^\x00-\x7F]') { throw 'APIShared package path contains non-ASCII characters; set SHCDE_API_SHARED_DIR explicitly.' }
    [IO.File]::WriteAllText($OutFile, $text, [Text.Encoding]::ASCII)
}

try {
    if ($env:SHCDE_BRANCH_API_ROOT) {
        $root = [IO.Path]::GetFullPath($env:SHCDE_BRANCH_API_ROOT)
        $commit = ((Invoke-ApiGit $root @('rev-parse','HEAD')) -join '').Trim()
        if ($commit -cne $env:SHCDE_BRANCH_API_COMMIT) { throw 'Selected branch SDK commit changed.' }
        $null = Assert-ApiConsumerPackage $Workspace (Join-Path $root 'BepInEx/plugins/APIShared_Serp')
        Write-Selection @(
            'set "SHCDE_API_SHARED_MAIN_SELECTION="',
            ('set "API_SHARED_DIR=' + (Join-Path $root 'BepInEx\plugins\APIShared_Serp') + '"'))
        exit 0
    }
    $Workspace = [IO.Path]::GetFullPath($Workspace).TrimEnd('\','/')
    $requested = @()
    foreach ($argument in @($DriverArguments)) {
        if ($argument -match '^/apishared:(.+)$') { $requested += $Matches[1].Trim().ToLowerInvariant() }
        elseif ($argument -match '^/apishared$') { throw 'Use /apishared:main or /apishared:dev.' }
    }
    if ($env:SHCDE_API_SHARED_BRANCH) { $requested += $env:SHCDE_API_SHARED_BRANCH.Trim().ToLowerInvariant() }
    $requested = @($requested | Where-Object { $_ } | Sort-Object -Unique)
    if ($requested.Count -gt 1) { throw "Conflicting APIShared branch requests: $($requested -join ', ')." }

    if ($requested.Count -eq 0) {
        # Release drivers pin their own SDK through inherited variables; keep them untouched.
        if ($env:SHCDE_API_SHARED_RELEASE_PROOF -or $env:SHCDE_API_SHARED_MAIN_SELECTION -or $env:SHCDE_API_SHARED_DIR) {
            Write-Host 'APIShared: using the SDK selected by the calling release driver.'
            Write-Selection @(); exit 0
        }
        $requested = @('dev')
        $implicit = $true
    } else { $implicit = $false }

    if ($env:SHCDE_API_SHARED_RELEASE_PROOF) {
        throw 'An explicit APIShared branch cannot override the release SDK pinned by the Workshop driver.'
    }

    switch ($requested[0]) {
        { $_ -in @('dev','local') } {
            $api = Assert-ApiCheckout $Workspace
            $branch = ((Invoke-ApiGit $api @('rev-parse','--abbrev-ref','HEAD')) -join '').Trim()
            $suffix = if ($implicit) { ' (default; pass /apishared:main for the release branch)' } else { '' }
            Write-Host "APIShared: local development checkout, branch '$branch'$suffix."
            Write-Selection @(
                'set "SHCDE_API_SHARED_MAIN_SELECTION="',
                ('set "API_SHARED_DIR=' + (Join-Path $api 'BepInEx\plugins\APIShared_Serp') + '"'))
        }
        'main' {
            # The game directory of the calling driver lets a fresh main worktree build.
            if (-not $env:SHCDE_GAME_DIR -and $env:GAME_DIR) { $env:SHCDE_GAME_DIR = $env:GAME_DIR }
            $selectionPath = Resolve-ApiMainSelection $Workspace
            $env:SHCDE_API_SHARED_MAIN_SELECTION = $selectionPath
            Ensure-ApiMainBuild $Workspace
            $selection = Assert-ApiMainSelection $Workspace $selectionPath
            $package = Join-Path $selection.Root 'BepInEx\plugins\APIShared_Serp'
            $version = ([IO.File]::ReadAllText((Join-Path $package 'info.json')) | ConvertFrom-Json).Version
            Write-Host "APIShared: main $($selection.Commit.Substring(0,12)) (version $version) from $package."
            Write-Selection @(
                ('set "SHCDE_API_SHARED_MAIN_SELECTION=' + $selectionPath + '"'),
                ('set "API_SHARED_DIR=' + $package + '"'))
        }
        default { throw "Unsupported APIShared branch '$($requested[0])'. Supported: main (release), dev (local checkout)." }
    }
    exit 0
} catch {
    Write-Host ('APIShared branch selection failed: ' + $_.Exception.Message) -ForegroundColor Red
    exit 1
}
