[CmdletBinding()]
param(
    [string[]]$LogPath = @('E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\LogOutput.log'),
    [string[]]$ExpectedMod = @(),
    [string]$OutputPath = ''
)
$ErrorActionPreference = 'Stop'
function Read-RuntimeHealthLog {
    param([string]$Path, [string[]]$Expected = @())
    $runs=[Collections.Generic.List[object]]::new(); $run=$null; $lineNumber=0
    try {
        foreach ($line in Get-Content -LiteralPath $Path) {
            $lineNumber++
            if ($null -eq $run -or $line -match '^\[Message:\s*BepInEx\].*BepInEx\s+.+\s+- Stronghold Crusader Definitive Edition') {
                $run=[pscustomobject]@{Run=$runs.Count+1;FirstLine=$lineNumber;FirstTime=$null;LastTime=$null;
                    Errors=[Collections.Generic.List[object]]::new();Warnings=[Collections.Generic.List[object]]::new()}
                $runs.Add($run)
            }
            if ($line -match '\[(?<time>20\d\d-\d\d-\d\d \d\d:\d\d:\d\d\.\d{3})\]') {
                if (-not $run.FirstTime) { $run.FirstTime=$Matches.time }; $run.LastTime=$Matches.time
            }
            if ($line -match '^\[(?<level>Error|Fatal|Warning)\s*:\s*(?<source>[^\]]+)\]\s*(?<detail>.*)$') {
                $item=[pscustomobject]@{Line=$lineNumber;Source=$Matches.source.Trim();Detail=$Matches.detail}
                if ($Matches.level -eq 'Warning') { $run.Warnings.Add($item) } else { $run.Errors.Add($item) }
            }
        }
    } catch { return [pscustomobject]@{Path=$Path;Status='Unavailable';Reason=$_.Exception.Message;Runs=@()} }
    $results=@(foreach ($item in $runs) {
        [pscustomobject]@{Run=$item.Run;FirstLine=$item.FirstLine;FirstTime=$item.FirstTime;LastTime=$item.LastTime;
            Status=$(if($item.Errors.Count){'Failed'}elseif($item.Warnings.Count){'WarningsDetected'}else{'NoFailuresLogged'});
            Errors=@($item.Errors.ToArray());WarningGroups=@($item.Warnings | Group-Object Source | ForEach-Object {
                [pscustomobject]@{Source=$_.Name;Count=$_.Count;Examples=@($_.Group | Select-Object -First 3)} });
            RequestedMods=$Expected;GameplayVerified=$false;
            Limit='Failure-only logging: silence does not verify gameplay, rendered effects, unexercised features or multiplayer. No success markers are required.'}
    })
    [pscustomobject]@{Path=$Path;Status=$(if($results.Count){'Read'}else{'Empty'});Runs=$results}
}
if ($MyInvocation.InvocationName -eq '.') { return }
$reports=@(foreach ($path in $LogPath) { Read-RuntimeHealthLog -Path $path -Expected $ExpectedMod })
$json=$reports | ConvertTo-Json -Depth 10
if ($OutputPath) { [IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath),$json.Replace("`r`n","`n").Replace("`n","`r`n")+"`r`n",[Text.UTF8Encoding]::new($false)) }
Write-Output $json
if (@($reports | Where-Object { $_.Status -eq 'Unavailable' -or @($_.Runs | Where-Object Status -eq 'Failed').Count }).Count) { exit 1 }
# A warning-free log is not gameplay acceptance; no success records are expected.
exit 2
