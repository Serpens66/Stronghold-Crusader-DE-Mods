# Private maintainer tooling; Invoke-CheckedCommand is provided by Release.Common.ps1.
function Get-VerifiedReleaseDraft([string]$Repository, [string]$Tag, [string]$Commit) {
    $result = Invoke-CheckedCommand -FilePath 'gh' -Arguments @('release','view',$Tag,'--repo',$Repository,'--json','isDraft,targetCommitish,url,assets') -AllowFailure
    $text = ($result.Output | ForEach-Object { [string]$_ }) -join "`n"
    if ($result.ExitCode -ne 0) {
        if ($result.ExitCode -eq 1 -and $text.Trim() -ceq 'release not found') { return $null }
        throw "Cannot determine GitHub release state for ${Tag}: $text"
    }
    $draft = $text | ConvertFrom-Json
    if (-not $draft.isDraft) { throw "Release already exists and is published: $Tag" }
    if ([string]$draft.targetCommitish -cne $Commit) { throw "Existing draft targets another commit: $($draft.targetCommitish)" }
    return $draft
}

function New-ResumableReleaseDraft([string]$Repository, [string]$Tag, [string]$Commit,
    [string]$Title, [string]$NotesPath, [string[]]$Assets) {
    for ($attempt = 1; $attempt -le 3; $attempt++) {
        # A failed create may still have created a draft. Never retry a write
        # until a fresh read confirms that no release exists under this tag.
        $draft = Get-VerifiedReleaseDraft $Repository $Tag $Commit
        if ($null -ne $draft) { return [pscustomobject]@{Created=$false;Draft=$draft} }
        $result = Invoke-CheckedCommand -FilePath 'gh' -Arguments (@('release','create',$Tag,'--repo',$Repository,'--draft','--target',$Commit,'--title',$Title,'--notes-file',$NotesPath) + $Assets) -AllowFailure
        if ($result.ExitCode -eq 0) { return [pscustomobject]@{Created=$true;Draft=$null} }
        $diagnostic = ($result.Output | ForEach-Object { [string]$_ }) -join "`n"
        $draft = Get-VerifiedReleaseDraft $Repository $Tag $Commit
        if ($null -ne $draft) { return [pscustomobject]@{Created=$false;Draft=$draft} }
        if ($attempt -eq 3 -or $diagnostic -notmatch 'HTTP (?:5\d\d|429)\b|timed out|connection reset|TLS handshake timeout') {
            throw "GitHub draft creation failed for $Tag (attempt $attempt/3, exit $($result.ExitCode)). Prepared assets remain available. $diagnostic"
        }
        Write-Host "Temporary GitHub failure creating $Tag; retry $($attempt + 1)/3 after checking release state."
        Start-Sleep -Seconds (2 * $attempt)
    }
}
