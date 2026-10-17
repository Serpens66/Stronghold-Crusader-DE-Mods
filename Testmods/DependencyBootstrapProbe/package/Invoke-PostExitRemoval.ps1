param([Parameter(Mandatory=$true)][string]$ReceiptPath,[switch]$Apply)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Removal.ps1')
try {
    Assert-ProbeUnlinked $ReceiptPath
    $receipt=[IO.File]::ReadAllText($ReceiptPath)|ConvertFrom-Json
    $result=Invoke-ProbeRemoval $receipt -Apply:$Apply
    $result|Add-Member -NotePropertyName AtUtc -NotePropertyValue ([DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ss.fffZ'))
    $result|ConvertTo-Json -Compress
    if($result.Status -eq 'Rejected'){exit 2}
}catch{[pscustomobject]@{Status='Rejected';Reason=$_.Exception.Message;AtUtc=[DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ss.fffZ')}|ConvertTo-Json -Compress;exit 2}
