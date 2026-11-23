param([string]$ResultPath = '')
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
. (Join-Path $workspace 'Shared\Tools\Paths\WorkPaths.ps1')
. (Join-Path $workspace 'APIShared\tools\BuildProof.ps1')
. (Join-Path $PSScriptRoot 'InstallTransaction.ps1')
$results = [Collections.Generic.List[string]]::new()
foreach ($name in @('Build.ps1','Install.ps1','InstallTransaction.ps1','Test-InstallMigration.ps1')) {
    $tokens = $null; $parseErrors = $null
    [void][Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot $name), [ref]$tokens, [ref]$parseErrors)
    if ($parseErrors.Count) { throw ($name + ': ' + ($parseErrors -join '; ')) }
}
# Exact former package bytes exercise the removal allowlist, including CRLF.
# The production matcher remains hash-only and never depends on this fixture source.
$legacy = @(
    'PFBhdGNoIHhtbG5zOm49Imh0dHA6Ly9zY2hlbWFzLm1pY3Jvc29mdC5jb20vd2luZngvMjAwNi94YW1sL3ByZXNlbnRhdGlvbiI+DQogIDxPcGVyYXRpb24gVHlwZT0iQWRkIiBYUGF0aD0iLy9uOkdyaWRbbjpCdXR0b25bQENvbW1hbmRQYXJhbWV0ZXI9J0NvbmZpcm1Hb29kcyddIGFuZCBuOkJ1dHRvbltAQ29tbWFuZFBhcmFtZXRlcj0nQ2FuY2VsR29vZHMnXV0iPg0KICAgIDxDb250ZW50Pg0KICAgICAgPENvbnRlbnRDb250cm9sIHhtbG5zOng9Imh0dHA6Ly9zY2hlbWFzLm1pY3Jvc29mdC5jb20vd2luZngvMjAwNi94YW1sIg0KICAgICAgICAgICAgICAgICAgICAgIHg6TmFtZT0iQXV0b1NlbmREaXNjbG9zdXJlSG9zdCIgTWFyZ2luPSIwLDAsMCw0Ig0KICAgICAgICAgICAgICAgICAgICAgIEhvcml6b250YWxBbGlnbm1lbnQ9IkNlbnRlciIgVmVydGljYWxBbGlnbm1lbnQ9IkJvdHRvbSIgVmlzaWJpbGl0eT0iQ29sbGFwc2VkIiAvPg0KICAgIDwvQ29udGVudD4NCiAgPC9PcGVyYXRpb24+DQo8L1BhdGNoPg0K',
    'PFBhdGNoIHhtbG5zOm49Imh0dHA6Ly9zY2hlbWFzLm1pY3Jvc29mdC5jb20vd2luZngvMjAwNi94YW1sL3ByZXNlbnRhdGlvbiI+DQogIDxPcGVyYXRpb24gVHlwZT0iQWRkIiBYUGF0aD0iLy9uOkdyaWRbbjpCdXR0b25bQENvbW1hbmRQYXJhbWV0ZXI9J0NvbmZpcm1Hb29kcyddIGFuZCBuOkJ1dHRvbltAQ29tbWFuZFBhcmFtZXRlcj0nQ2FuY2VsR29vZHMnXV0iPg0KICAgIDxDb250ZW50Pg0KICAgICAgPENvbnRlbnRDb250cm9sIHhtbG5zOng9Imh0dHA6Ly9zY2hlbWFzLm1pY3Jvc29mdC5jb20vd2luZngvMjAwNi94YW1sIg0KICAgICAgICAgICAgICAgICAgICAgIHg6TmFtZT0iQXV0b1NlbmREaXNjbG9zdXJlSG9zdCIgTWFyZ2luPSIxNiwwLDAsNCINCiAgICAgICAgICAgICAgICAgICAgICBIb3Jpem9udGFsQWxpZ25tZW50PSJMZWZ0IiBWZXJ0aWNhbEFsaWdubWVudD0iQm90dG9tIiBWaXNpYmlsaXR5PSJDb2xsYXBzZWQiIC8+DQogICAgPC9Db250ZW50Pg0KICA8L09wZXJhdGlvbj4NCjwvUGF0Y2g+DQo=')
function Require([bool]$Value, [string]$Reason) { if (!$Value) { throw $Reason } }
function Write-FixtureText([string]$Path, [string]$Text) {
    [void][IO.Directory]::CreateDirectory((Split-Path -Parent $Path))
    [IO.File]::WriteAllText($Path, $Text, [Text.UTF8Encoding]::new($false))
}
function Inventory([string]$Path) {
    @(Get-ChildItem -LiteralPath $Path -Recurse -File | Sort-Object FullName | ForEach-Object {
        $_.FullName.Substring($Path.Length) + '|' + (Get-ApiFileHash $_.FullName)
    })
}
function Expect-Failure([scriptblock]$Action, [string]$Message) {
    $caught = $null
    try { & $Action | Out-Null } catch { $caught = $_ }
    Require ($null -ne $caught) 'Expected a failing installation operation.'
    Require ($caught.Exception.Message -like ('*' + $Message + '*')) ('Unexpected failure: ' + $caught.Exception.Message)
}
function New-Case([string]$Name, [byte[]]$Patch) {
    $root = New-ShortWorkDirectory -Root $workspace -Kind t -Name 'autosend-install' -Identity $Name -Transient
    $game = Join-Path $root 'game'
    $evidence = Join-Path $root 'evidence'; [void][IO.Directory]::CreateDirectory($evidence)
    $relative = 'BepInEx\plugins\AutoSendTest_Serp\AutoSendTest.dll'
    $target = Join-Path $game $relative
    $source = Join-Path $root 'payload\AutoSendTest.dll'
    Write-FixtureText $source 'new normal artifact'
    Write-FixtureText $target 'old normal artifact'
    Write-FixtureText (Join-Path $game 'BepInEx\plugins\AutoSendTest_Serp\user.json') 'owned-folder user data'
    Write-FixtureText (Join-Path $game 'BepInEx\plugins\Independent\settings.txt') 'unrelated plugin'
    Write-FixtureText (Join-Path $game 'BepInEx\patchers\Independent.dll') 'unrelated patcher'
    $old = Join-Path $game (Get-AutoSendLegacyPatchRelativePath)
    if ($null -ne $Patch) {
        [void][IO.Directory]::CreateDirectory((Split-Path -Parent $old)); [IO.File]::WriteAllBytes($old, $Patch)
    }
    [pscustomobject]@{Root=$root;Game=$game;Evidence=$evidence;Old=$old;Target=$target;Source=$source;
        Payload=@([pscustomobject]@{Relative=$relative;Target=$target;Source=$source;Sha256=(Get-ApiFileHash $source)})}
}
try {
    foreach ($encoded in $legacy) {
        $case = New-Case 'recognised patch removal' ([Convert]::FromBase64String($encoded))
        $oldHash = Get-ApiFileHash $case.Old
        $plan = New-AutoSendInstallPlan -GameDir $case.Game -Payload $case.Payload
        Require ($plan.RemovedFiles.Count -eq 1 -and $plan.Targets.Contains($case.Old)) 'Removal must be an owned target for inventory comparison.'
        $unrelatedBefore = @(Inventory $case.Game | Where-Object { $_ -notlike '*AutoSendTest.dll|*' -and $_ -notlike '*HUD_AlliesPanel.xaml|*' })
        $done = Invoke-AutoSendInstallTransaction -Plan $plan -EvidenceDirectory $case.Evidence -ValidateAfter {
            Require (!(Test-Path -LiteralPath $case.Old)) 'Known obsolete patch was not removed.'
            Require ((Get-ApiFileHash $case.Target) -eq (Get-ApiFileHash $case.Source)) 'Payload was not installed.'
        }
        Require ($done.RemovedFiles.Count -eq 1 -and $done.RemovedFiles[0].Sha256 -eq $oldHash) 'Removal evidence lost its original identity.'
        $unrelatedAfter = @(Inventory $case.Game | Where-Object { $_ -notlike '*AutoSendTest.dll|*' -and $_ -notlike '*HUD_AlliesPanel.xaml|*' })
        Require (!(Compare-Object $unrelatedBefore $unrelatedAfter)) 'Unrelated files changed.'
        Require ((Get-ApiFileHash (Join-Path $case.Evidence ('backup\' + (Get-AutoSendLegacyPatchRelativePath)))) -eq $oldHash) 'Removed patch backup differs.'
        $results.Add('known-patch-' + $oldHash + ': PASS')
    }
    $case = New-Case 'unknown patch aborts without any writes' ([Text.Encoding]::UTF8.GetBytes('user edited XAML'))
    $before = Inventory $case.Root
    Expect-Failure { New-AutoSendInstallPlan -GameDir $case.Game -Payload $case.Payload } 'unknown or edited'
    Require (!(Compare-Object $before (Inventory $case.Root))) 'Unknown patch rejection wrote fixture/evidence/installed files.'
    $results.Add('unknown-before-any-writes: PASS')

    $case = New-Case 'reject own-prefix traversal before writes' ([Convert]::FromBase64String($legacy[0]))
    $before = Inventory $case.Root
    $escapedRelative = 'BepInEx\plugins\AutoSendTest_Serp\..\Independent\settings.txt'
    $escaped = [pscustomobject]@{Relative=$escapedRelative;Target=([IO.Path]::GetFullPath((Join-Path $case.Game $escapedRelative)));
        Source=$case.Source;Sha256=(Get-ApiFileHash $case.Source)}
    Expect-Failure { New-AutoSendInstallPlan -GameDir $case.Game -Payload @($escaped) } 'Unsafe owned installation path'
    Require (!(Compare-Object $before (Inventory $case.Root))) 'Rejected traversal wrote fixture files.'
    $results.Add('owned-target-traversal-rejected: PASS')

    $case = New-Case 'later real IO failure restores deleted patch and prior payload' ([Convert]::FromBase64String($legacy[0]))
    $blocked = Join-Path $case.Game 'BepInEx\plugins\AutoSendTest_Serp\blocked'
    Write-FixtureText $blocked 'a file cannot be a destination directory'
    $before = Inventory $case.Game
    $secondRelative = 'BepInEx\plugins\AutoSendTest_Serp\blocked\second.dll'
    $bad = [pscustomobject]@{Relative=$secondRelative;Target=(Join-Path $case.Game $secondRelative);Source=$case.Source;Sha256=(Get-ApiFileHash $case.Source)}
    $plan = New-AutoSendInstallPlan -GameDir $case.Game -Payload @($case.Payload[0],$bad)
    Expect-Failure { Invoke-AutoSendInstallTransaction -Plan $plan -EvidenceDirectory $case.Evidence -ValidateAfter { throw 'Should not reach validation.' } } 'directory'
    Require (!(Compare-Object $before (Inventory $case.Game))) 'Rollback after a later IO error failed to restore the exact installed file inventory.'
    $results.Add('later-IO-failure-restores-removal-and-overwrite: PASS')

    $case = New-Case 'late SDK/inventory/finalisation failure rolls back' ([Convert]::FromBase64String($legacy[1]))
    $before = Inventory $case.Game
    $newRelative = 'BepInEx\plugins\AutoSendTest_Serp\new-locale.txt'
    $newEntry = [pscustomobject]@{Relative=$newRelative;Target=(Join-Path $case.Game $newRelative);Source=$case.Source;Sha256=(Get-ApiFileHash $case.Source)}
    $plan = New-AutoSendInstallPlan -GameDir $case.Game -Payload @($case.Payload[0],$newEntry)
    $observed = [Collections.Generic.List[bool]]::new()
    Expect-Failure { Invoke-AutoSendInstallTransaction -Plan $plan -EvidenceDirectory $case.Evidence -ValidateAfter {
        $observed.Add(!(Test-Path -LiteralPath $case.Old) -and (Test-Path -LiteralPath $newEntry.Target) -and
            (Get-ApiFileHash $case.Target) -eq (Get-ApiFileHash $case.Source))
        throw 'late verification failure'
    } } 'late verification failure'
    Require ($observed.Count -eq 1 -and $observed[0]) 'The late verifier did not observe the changed installation before rollback.'
    Require (!(Compare-Object $before (Inventory $case.Game))) 'Late validation rollback changed installed files.'
    $results.Add('late-validation-failure-restores-exact-inventory: PASS')

    $case = New-Case 'fresh and already migrated installs need no removal' $null
    $plan = New-AutoSendInstallPlan -GameDir $case.Game -Payload $case.Payload
    Require ($plan.RemovedFiles.Count -eq 0) 'A missing obsolete file must not manufacture a removal.'
    $done = Invoke-AutoSendInstallTransaction -Plan $plan -EvidenceDirectory $case.Evidence -ValidateAfter {}
    Require ($done.RemovedFiles.Count -eq 0 -and !(Test-Path -LiteralPath $case.Old)) 'Fresh install removal evidence is wrong.'
    $results.Add('no-obsolete-file: PASS')
    if ($ResultPath) {
        [ordered]@{ObservedUtc=[DateTime]::UtcNow.ToString('o');Passed=$true;PowerShell=$PSVersionTable.PSVersion.ToString();
            Scope='Offline production transaction/file fixtures only; no game or SDK installation.';Cases=$results.ToArray()} |
            ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $ResultPath -Encoding UTF8
    }
    Write-Host ('PASS: ' + $results.Count + ' offline AutoSend installation migration cases.')
} finally { Complete-TransientWorkDirectories -Owner $PSCommandPath }
