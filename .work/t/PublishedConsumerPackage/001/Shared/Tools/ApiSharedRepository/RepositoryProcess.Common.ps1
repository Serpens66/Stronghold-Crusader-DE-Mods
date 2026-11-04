# No shell interpolation: GitHub/Git diagnostics are data, including in Windows PowerShell 5.1.
function Invoke-ApiRepositoryProcess([string]$Executable, [string[]]$Arguments) {
    $command=Get-Command $Executable -CommandType Application -ErrorAction Stop | Select-Object -First 1
    $quoted=@(foreach($argument in $Arguments) {
        $escaped=[regex]::Replace($argument,'(\\*)"','$1$1\"')
        '"'+[regex]::Replace($escaped,'(\\+)$','$1$1')+'"'
    })
    $start=[Diagnostics.ProcessStartInfo]::new()
    $start.FileName=$command.Source;$start.Arguments=$quoted -join ' '
    $start.UseShellExecute=$false;$start.CreateNoWindow=$true
    $start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    $start.StandardOutputEncoding=[Text.UTF8Encoding]::new($false)
    $start.StandardErrorEncoding=[Text.UTF8Encoding]::new($false)
    $process=[Diagnostics.Process]::new();$process.StartInfo=$start
    try {
        if(-not $process.Start()){throw "Cannot start $Executable"}
        $output=$process.StandardOutput.ReadToEndAsync();$diagnostic=$process.StandardError.ReadToEndAsync()
        $process.WaitForExit()
        return [pscustomobject]@{ExitCode=$process.ExitCode;Output=$output.Result;Diagnostic=$diagnostic.Result.Trim()}
    } finally {$process.Dispose()}
}
function Invoke-ApiRepositoryGit([string]$Root,[string[]]$Arguments) {
    $result=Invoke-ApiRepositoryProcess 'git' (@('-C',$Root)+$Arguments)
    if($result.ExitCode -ne 0){throw "Git failed ($($Arguments[0])): $($result.Diagnostic)"}
    return @($result.Output -split '\r?\n' | Where-Object {$_ -ne ''})
}
function Assert-ApiRepositoryOrigin([string]$Root) {
    $origin=(Invoke-ApiRepositoryGit $Root @('remote','get-url','origin')) -join ''
    if($origin -notmatch '^https://github\.com/SHCDE-APIShared/APIShared(?:\.git)?/?$|^git@github\.com:SHCDE-APIShared/APIShared\.git$') {
        throw 'APIShared origin must be the canonical public repository.'
    }
}
function Get-ApiRepositoryHash([string]$Path) {
    $stream=$null;$sha=$null
    try {$stream=[IO.File]::OpenRead($Path);$sha=[Security.Cryptography.SHA256]::Create();return ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-','').ToLowerInvariant()}
    finally {if($sha){$sha.Dispose()};if($stream){$stream.Dispose()}}
}
