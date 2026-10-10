$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\Fremde Mods\crusader-de-tweaker-api-review'))
function Edit([string]$path, [scriptblock]$change) {
    $full = Join-Path $repo $path
    $before = [IO.File]::ReadAllText($full).Replace("`r`n", "`n")
    $after = (& $change $before).Replace("`r`n", "`n").Replace("`n", "`r`n")
    [IO.File]::WriteAllText($full, $after, [Text.UTF8Encoding]::new($false))
    if ([IO.File]::ReadAllText($full) -cne $after) { throw "Readback failed: $path" }
}
function ReplaceOnce([string]$s, [string]$old, [string]$new) {
    if (!$s.Contains($old)) { throw "Missing edit anchor: $old" }
    return $s.Replace($old, $new)
}
Edit 'Config\PublicApi\ConfigurationFileTransaction.cs' {
    param($s)
    $s = ReplaceOnce $s 'private readonly string identity;' 'private readonly string identity;
        private readonly string lobbyPath;
        private readonly string channel;'
    $s = $s.Replace('"PendingConfiguration.bin"','channel + "PendingConfiguration.bin"').Replace('"ApplyingConfiguration.bin"','channel + "ApplyingConfiguration.bin"').Replace('"LastAppliedConfiguration.bin"','channel + "LastAppliedConfiguration.bin"')
    $s = ReplaceOnce $s 'string schemaIdentity)' 'string schemaIdentity, string lobbyStoragePath = null, bool immediate = false)'
    $s = ReplaceOnce $s 'identity = schemaIdentity;' 'identity = schemaIdentity;
            lobbyPath = lobbyStoragePath == null ? null : Path.GetFullPath(lobbyStoragePath);
            channel = immediate ? "Immediate" : "";'
    $s = ReplaceOnce $s 'string target = Path.GetFullPath(Path.Combine(root, name));' '// The caller supplies one exact plugin-owned destination; packages contain only logical names.
            if (name == "LobbySettings.msgpack" && lobbyPath != null) return lobbyPath;
            string target = Path.GetFullPath(Path.Combine(root, name));'
    $s = ReplaceOnce $s 'private static void AtomicWrite(string path, byte[] bytes)' 'internal static void AtomicWrite(string path, byte[] bytes)'
    $s
}
Edit 'Config\PublicApi\ConfigurationContracts.cs' {
    param($s)
    $s = ReplaceOnce $s 'public bool RequiresRestart { get { return true; } }' 'public bool RequiresRestart { get; internal set; } = true;'
    $s = ReplaceOnce $s 'public bool CanApplyWithoutRestart { get { return false; } }' 'public bool CanApplyWithoutRestart { get { return false; } }
        /// <summary>ApplyLiveConfiguration accepts a full snapshot only if its startup-only values are unchanged.</summary>
        public bool CanApplyLiveSubset { get; internal set; }'
    $s
}
Edit 'Config\PublicApi\ConfigurationDocuments.cs' {
    param($s)
    $anchor = '        internal Dictionary<string, object> Read(IDictionary<string, byte[]> files)'
    $s = ReplaceOnce $s $anchor '        // Scalar adapters reuse a loader parser for options with several legal TOML representations.
        internal void BindTextValue(string file, string group, string name,
            Func<object, string> read, Func<string, object> write)
        {
            if (!validators.TryGetValue(file + "\0" + name, out var candidates)) return;
            foreach (Cell cell in candidates.Where(x => x.Path[0] == group))
            {
                cell.Option.DefaultValue = read(cell.Option.DefaultValue);
                cell.Option.ValueType = "String";
                cell.Option.Minimum = null;
                cell.Option.Maximum = null;
                cell.Type = typeof(string);
                cell.ReadValue = read;
                cell.WriteValue = value => write((string)value);
                cell.Validate = value => { write((string)value); return true; };
            }
        }

        internal Dictionary<string, object> Read(IDictionary<string, byte[]> files)'
    $s = ReplaceOnce $s 'result.Add(cell.Option.Key, Normalize(node));' 'result.Add(cell.Option.Key, cell.ReadValue == null ? Normalize(node) : cell.ReadValue(node));'
    $s = ReplaceOnce $s 'current[cell.Path[cell.Path.Length - 1]] = Normalize(values[cell.Option.Key]);' 'object value = values[cell.Option.Key];
                        current[cell.Path[cell.Path.Length - 1]] = cell.WriteValue == null ? Normalize(value) : cell.WriteValue(value);'
    $s = ReplaceOnce $s 'internal Func<object, bool> Validate;' 'internal Func<object, bool> Validate;
            internal Func<object, string> ReadValue;
            internal Func<object, object> WriteValue;'
    $s
}
