function Resolve-WorkshopOptions([string[]]$Options, [string]$Selection) {
    $targets=@();$validate=$false;$test=$false;$noPause=$false
    foreach($option in $Options){switch($option.ToLowerInvariant()){
        '/modpack'{$targets+='SerpsMods'} '/apishared'{$targets+='APIShared'} '/both'{$targets+='Both'}
        '/validate'{$validate=$true} '/test'{$test=$true} '/nopause'{$noPause=$true}
        default{throw "Unknown option: $option"}
    }}
    if($targets.Count -gt 1){throw 'Specify exactly one target option.'}
    if($targets.Count -eq 0){
        if(-not $Selection){Write-Host "1. SerpsMods`r`n2. APIShared`r`n3. Beide`r`n0. Abbrechen";$Selection=Read-Host 'Auswahl'}
        $targets=@(switch($Selection){'1'{'SerpsMods'} '2'{'APIShared'} '3'{'Both'} '0'{'Cancel'} default{throw 'Invalid target selection.'}})
    }
    [pscustomobject]@{Target=$targets[0];Validate=$validate;Test=$test;NoPause=$noPause}
}
