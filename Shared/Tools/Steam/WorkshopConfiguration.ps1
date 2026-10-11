function Get-WorkshopConfiguration {
    $root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
    [pscustomobject]@{
        Root=$root;AppId=3024040;Packager='AUTO'
        Mods=@('BugfixesAndQoL','BuildingCosts','BuildingLimit','ExtraFeatures','RandomEvents','CastlePlanner','StartConditions','UnitCosts','UnitLimit','ExtendedData')
        SerpsMods=[pscustomobject]@{Key='SerpsMods';Guid='SerpsMods_Serp';Title='Serps Mod';Folder=(Join-Path $root 'SerpsMods');Preview=(Join-Path $root 'SerpsModsHost/steam-preview.png');ItemId='';Visibility='Public'}
        APIShared=[pscustomobject]@{Key='APIShared';Guid='APIShared_Serp';Title='APIShared';Folder=(Join-Path $root 'APISharedWorkshop');Preview=(Join-Path $root 'SerpsModsHost/steam-preview.png');ItemId='';Visibility='Public'}
    }
}
