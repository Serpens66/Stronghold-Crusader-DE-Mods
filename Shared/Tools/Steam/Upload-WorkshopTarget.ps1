param([int]$TestMode=0,[int]$ValidateOnly=0,[ValidateSet('SerpsMods','APIShared')][string]$Target)
$arguments=@('-NoProfile','-ExecutionPolicy','Bypass','-File',(Join-Path $PSScriptRoot 'Upload-Workshop.ps1'),'-Target',$Target,'-TestMode',$TestMode)
if($ValidateOnly){$arguments+='-Validate'}
& powershell.exe @arguments
exit $LASTEXITCODE
