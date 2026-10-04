$ErrorActionPreference='Stop'
$root=Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) '..\Fremde Mods\crusader-de-tweaker'
function EditFile($relative,$old,$new) {
 $path=Join-Path $root $relative
 $text=[IO.File]::ReadAllText($path).Replace("`r`n","`n")
 $old=$old.Replace("`r`n","`n"); $new=$new.Replace("`r`n","`n")
 if(!$text.Contains($old)){throw "Missing anchor: $relative"}
 $text=$text.Replace($old,$new).Replace("`n","`r`n")
 [IO.File]::WriteAllText($path,$text,[Text.UTF8Encoding]::new($false))
 if([IO.File]::ReadAllText($path) -cne $text){throw "Write verification: $relative"}
}
EditFile 'Config/PublicApi/ConfigurationApi.cs' '        public static event Action Changed;' @"
        public static event Action Changed;
        internal static Func<bool> NetworkClientCheck;
        internal static Func<bool> NetworkPreparation;
        public static bool IsNetworkConfigurationClient() => NetworkClientCheck?.Invoke() ?? false;
        public static bool PrepareNetworkConfiguration() => NetworkPreparation?.Invoke() ?? true;

        // The existing authenticated host transport supplies a complete file set. Local diagnostic
        // settings stay personal. Receiving data is not evidence that startup values are active.
        internal static bool PrepareReceivedConfiguration(Dictionary<string, byte[]> files,
            Dictionary<string, float> multipliers, string contextId, bool stage)
        {
            lock (Gate)
            {
                RequireReady();
                var own = SelectFiles(transaction.ReadOwn(), false);
                var candidateFiles = new Dictionary<string, byte[]>(own, StringComparer.Ordinal);
                foreach (string name in Inventory().Where(x => x != CfgName))
                {
                    if (!files.TryGetValue(name, out var bytes) || bytes == null)
                        throw new InvalidDataException("Host configuration is incomplete: " + name);
                    candidateFiles[name] = bytes;
                }
                var desired = documents.Read(candidateFiles);
                foreach (var option in documents.Options.Where(x => x.File == CfgName && !x.IsLocal))
                {
                    if (option.Group != "Multipliers" || !multipliers.TryGetValue(option.Name, out float value))
                        throw new InvalidDataException("Host configuration is incomplete: " + option.Key);
                    desired[option.Key] = (double)value;
                }
                var problems = documents.Validate(desired);
                if (problems.Length != 0) throw new InvalidDataException(string.Join("; ", problems.Select(x => x.ToString())));
                bool matches = documents.Options.Where(x => !x.IsLocal).All(x => Equals(desired[x.Key], loaded.Values[x.Key]));
                if (matches) return true;
                if (stage) StageContextConfiguration(desired, contextId, ConfigurationFileTransaction.Revision(own));
                return false;
            }
        }
"@
EditFile 'Config/Sync/ConfigSyncManager.cs' '            BuildLocalPackage();' @"
            BuildLocalPackage();
            Configuration.ConfigurationApi.NetworkClientCheck = () => CurrentRole() == Role.Client;
            Configuration.ConfigurationApi.NetworkPreparation = PrepareNetworkRestart;
"@
EditFile 'Config/Sync/ConfigSyncManager.cs' '        private static void ApplyHost(ConfigSyncPackage package)' @"
        private static bool CheckNetworkRestart(ConfigSyncPackage package, bool stage)
        {
            if (ConfigSyncCodec.CompareVersions(package.HostModVersion, PluginInfo.PLUGIN_VERSION) == ConfigSyncCodec.VersionVerdict.Incompatible)
                throw new InvalidDataException("The host's Tweaker version is incompatible.");
            var files = package.Entries.Where(x => x.Key != SyncFileId.GlobalMultipliers)
                .ToDictionary(x => ConfigSyncCodec.RelativePathFor(x.Key), x => x.Value, StringComparer.Ordinal);
            if (!package.Entries.TryGetValue(SyncFileId.GlobalMultipliers, out var bytes) ||
                !ConfigSyncCodec.TryParseMultipliers(bytes, out var multipliers, out string error))
                throw new InvalidDataException("The host's multipliers are missing or invalid.");
            string context = "network:" + HostIdText() + ":" + BitConverter.ToString(package.BodyHash).Replace("-", "");
            return Configuration.ConfigurationApi.PrepareReceivedConfiguration(files, multipliers, context, stage);
        }

        private static bool PrepareNetworkRestart()
        {
            if (CurrentRole() != Role.Client) return true;
            if (_hostSyncEnabled == false) return true;
            if (_hostSyncEnabled != true || _hostPackage == null || _hostPackageError != null)
                throw new InvalidOperationException("Waiting for a valid configuration from the lobby owner.");
            bool ready = CheckNetworkRestart(_hostPackage, true);
            if (!ready)
            {
                _problem = "Host settings prepared. Restart the game, rejoin this host and select Ready again.";
                UpdateStatus();
            }
            return ready;
        }

        private static void ApplyHost(ConfigSyncPackage package)
"@
EditFile 'Config/Sync/ConfigSyncManager.cs' '                var written = WriteHostFiles(package);' @"
                if (!CheckNetworkRestart(package, false))
                {
                    _applied = false;
                    _appliedHash = null;
                    _problem = "These host settings require a restart. Select Ready to prepare them, then restart and rejoin this host.";
                    UpdateStatus();
                    return;
                }
                var written = WriteHostFiles(package);
"@