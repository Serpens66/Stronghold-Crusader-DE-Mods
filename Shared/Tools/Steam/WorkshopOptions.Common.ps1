function Resolve-WorkshopOptions([string[]]$Options, [string]$Selection) {
    $branch=$null;$targets=@();$validate=$false;$test=$false;$noPause=$false
    foreach($option in $Options){switch($option.ToLowerInvariant()){
        '/modpack'{$targets+='SerpsMods'} '/apishared'{$targets+='APIShared'} '/both'{$targets+='Both'}
        '/validate'{$validate=$true} '/test'{$test=$true} '/nopause'{$noPause=$true}
        '/branch:main'{if($branch){throw 'Specify one modpack branch.'};$branch='main'} '/branch:develop'{if($branch){throw 'Specify one modpack branch.'};$branch='develop'}
        default{throw "Unknown option: $option"}
    }}
    if($targets.Count -gt 1){throw 'Specify exactly one target option.'}
    if($targets.Count -eq 0){
        if(-not $Selection){Write-Host "1. SerpsMods`r`n2. APIShared`r`n3. Beide`r`n0. Abbrechen";$Selection=Read-Host 'Auswahl'}
        $targets=@(switch($Selection){'1'{'SerpsMods'} '2'{'APIShared'} '3'{'Both'} '0'{'Cancel'} default{throw 'Invalid target selection.'}})
    }
    if($branch -and $targets[0] -eq 'APIShared'){throw '/branch selects the modpack branch and requires /modpack or /both.'}
    [pscustomobject]@{Branch=$branch;Target=$targets[0];Validate=$validate;Test=$test;NoPause=$noPause}
}
