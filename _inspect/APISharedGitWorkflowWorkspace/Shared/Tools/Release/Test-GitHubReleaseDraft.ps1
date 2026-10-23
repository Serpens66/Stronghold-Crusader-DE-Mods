Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'GitHubReleaseDraft.ps1')
function Check([bool]$Condition,[string]$Message) { if (-not $Condition) { throw $Message } }
function Invoke-CheckedCommand {
    param($FilePath,$Arguments,[switch]$AllowFailure)
    Check ($FilePath -ceq 'gh' -and $AllowFailure) 'Unexpected command adapter.'
    $script:calls.Add(($Arguments -join '|'))
    Check ($script:responses.Count -gt 0) 'Unexpected extra GitHub call.'
    $response = $script:responses.Dequeue()
    if ($response -eq 'success') { return @{ExitCode=0;Output=@()} }
    if ($response -in @('draft','foreign','published')) {
        return @{ExitCode=0;Output=@(@{isDraft=($response -ne 'published');targetCommitish=$(if($response -eq 'foreign'){'other'}else{'commit'});url='fixture';assets=@()}|ConvertTo-Json -Compress)}
    }
    $text = switch ($response) {
        'missing' {'release not found'} '500' {'HTTP 500 (fixture)'}
        '403' {'HTTP 403: Forbidden'} 'timeout' {'connection timed out'}
        'ambiguous' {"release not found`nHTTP 401"} default {throw 'Unknown fixture response.'}
    }
    return @{ExitCode=1;Output=@($text)}
}
function Start-Sleep { param($Seconds) $script:delays.Add([int]$Seconds) }
function Run-Case([string]$Name,[string[]]$Responses,[int]$Creates,[bool]$Reject,[bool]$Recovered=$false) {
    $script:responses=[Collections.Generic.Queue[string]]::new()
    foreach($response in $Responses){$script:responses.Enqueue($response)}
    $script:calls=[Collections.Generic.List[string]]::new();$script:delays=[Collections.Generic.List[int]]::new()
    $failed=$false;$result=$null
    try {$result=New-ResumableReleaseDraft 'Fixture/Mods' 'ExtraFeatures/v1.0.113' 'commit' 'Fixture title' 'path with spaces/notes.txt' @('path with spaces/mod.zip')}
    catch {$failed=$true}
    Check ($failed -eq $Reject) ("Unexpected result: $Name")
    Check (@($script:calls|Where-Object {$_ -like 'release|create|*'}).Count -eq $Creates) ("Wrong create count: $Name")
    Check ($script:responses.Count -eq 0) ("Unconsumed responses: $Name")
    if(-not $Reject){Check ($result.Created -eq (-not $Recovered)) ("Wrong recovery result: $Name")}
    foreach($call in $script:calls|Where-Object {$_ -like 'release|create|*'}) {
        Check ($call -like '*|--draft|--target|commit|*|--notes-file|path with spaces/notes.txt|path with spaces/mod.zip') 'Create arguments changed.'
    }
}
Run-Case 'success' @('missing','success') 1 $false
Run-Case '500 then success' @('missing','500','missing','missing','success') 2 $false
Check (($script:delays -join ',') -ceq '2') 'Incorrect first retry delay.'
Run-Case 'timeout then success' @('missing','timeout','missing','missing','success') 2 $false
Run-Case 'created despite HTTP 500' @('missing','500','draft') 1 $false $true
Run-Case 'existing matching draft' @('draft') 0 $false $true
Run-Case 'foreign draft' @('foreign') 0 $true
Run-Case 'published release' @('published') 0 $true
Run-Case 'permissions failure' @('missing','403','missing') 1 $true
Run-Case 'unreadable state' @('403') 0 $true
Run-Case 'ambiguous missing response' @('ambiguous') 0 $true
Run-Case 'unknown result after create' @('missing','500','403') 1 $true
Run-Case 'bounded retries' @('missing','500','missing','missing','500','missing','missing','500','missing') 3 $true
Check (($script:delays -join ',') -ceq '2,4') 'Retry count/delays are not bounded.'
Write-Host 'PASS: transient GitHub failures, bounded retries, ambiguous create recovery, commit ownership, published releases, permissions and strict missing detection; no network writes.'
