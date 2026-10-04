$ErrorActionPreference='Stop'
$root=Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) '..\Fremde Mods\crusader-de-tweaker'
$p=Join-Path $root 'Config/Sync/ConfigSyncManager.cs'
$t=[IO.File]::ReadAllText($p)
$old='            if (_hostSyncEnabled == false) return true;'
$new=@"
            if (_hostSyncEnabled == false)
            {
                var own = Configuration.ConfigurationApi.ReadOwnConfiguration();
                var active = Configuration.ConfigurationApi.GetLoadedConfiguration();
                if (own.Values.All(x => active.Values.TryGetValue(x.Key, out var value) && Equals(value, x.Value))) return true;
                Configuration.ConfigurationApi.StageReturnToOwnConfiguration();
                _problem = "Personal settings prepared. Restart the game and rejoin this lobby.";
                UpdateStatus();
                return false;
            }
"@
if(!$t.Contains($old)){throw 'Network off anchor missing'}
$t=$t.Replace($old,$new);$t=[regex]::Replace($t,"`r?`n","`r`n")
[IO.File]::WriteAllText($p,$t,[Text.UTF8Encoding]::new($false))
$p=Join-Path $root 'Config/PublicApi/ConfigurationApi.cs'
$t=[IO.File]::ReadAllText($p)
$t=$t.Replace('                using (var reader = new BinaryReader(File.OpenRead(existing))) { reader.ReadInt32(); identity = reader.ReadString(); }',@"
                using (var stream = File.OpenRead(existing))
                {
                    if (stream.Length > ConfigurationFileTransaction.MaximumPackageBytes)
                        throw new InvalidDataException("Configuration package is too large.");
                    using (var reader = new BinaryReader(stream))
                    {
                        if (reader.ReadInt32() != 0x43545031) throw new InvalidDataException("Invalid configuration package header.");
                        identity = reader.ReadString();
                    }
                }
"@)
$t=[regex]::Replace($t,"`r?`n","`r`n");[IO.File]::WriteAllText($p,$t,[Text.UTF8Encoding]::new($false))